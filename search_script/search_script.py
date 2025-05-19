# Для измерения затраченного времени
from time import time

from typing import Any
# Для запуска скрипта из командной строки с аргументами
from sys import argv

# Для работы с массивами
import numpy as np

# Для работы с las-файлами
import laspy
from laspy import LasData
from numpy import ndarray, dtype

# Для работы с shp-файлами(для назначения породы)
import geopandas as gpd
from shapely.geometry import Point
import pandas as pd

# Для работы с R
from rpy2 import robjects
from rpy2.robjects.packages import importr

# Для работы с GPU
import pycuda.autoinit
import pycuda.driver as drv
from pycuda.compiler import SourceModule

# Функции для поиска деревьев и определения их высот

mod = SourceModule('''
__global__ void kernel_clusterization(int n, float d, float *x, float *y, float *out_x, float *out_y)
{
    const float r2 = (d * d) / 4.0f;
    const int step = blockDim.x * gridDim.x;

    float vx = 0.0f;
    float vy = 0.0f;

    float sum_x = 0.0f;
    float sum_y = 0.0f;
    float sum_m = 0.0f;

    for (int i = blockIdx.x * blockDim.x + threadIdx.x; i < n; i += step)
    {
        sum_x = 0.0f;
        sum_y = 0.0f;
        sum_m = 0.0f;
        for (int j = 0; j < n; j++)
        {
            vx = x[j] - x[i];
            vy = y[j] - y[i];

            if (vx * vx + vy * vy <= r2)
            {
                sum_x += vx;
                sum_y += vy;
                sum_m += 1;
            }
        }
        out_x[i] = x[i] + sum_x / sum_m;
        out_y[i] = y[i] + sum_y / sum_m;
    }
}

__global__ void kernel_neighbors(int n, float d, float *x, float *y, float *z, float *out_m)
{
    const float r2 = (d * d) / 4.0f;
    const int step = blockDim.x * gridDim.x;

    float vx = 0.0f;
    float vy = 0.0f;

    float sum_m = 0.0f;

    for (int i = blockIdx.x * blockDim.x + threadIdx.x; i < n; i += step)
    {
        sum_m = 0.0f;
        for (int j = 0; j < n; j++)
        {
            vx = x[j] - x[i];
            vy = y[j] - y[i];

            if (vx * vx + vy * vy <= r2 && z[j] <= z[i])
            {
                sum_m += 1;
            }
        }
        out_m[i] = sum_m;
    }
}

__global__ void kernel_lmf(int n, float d, float *x, float *y, float *z, bool *out)
{
    const float r2 = (d * d) / 4.0f;
    const int step = blockDim.x * gridDim.x;

    float vx = 0.0f;
    float vy = 0.0f;

    for (int i = blockIdx.x * blockDim.x + threadIdx.x; i < n; i += step)
    {
        for (int j = 0; j < n; j++)
        {
            vx = x[j] - x[i];
            vy = y[j] - y[i];

            if (vx * vx + vy * vy <= r2 && z[i] < z[j])
            {
                out[i] = false;
                break;
            }
        }
    }
}

__global__ void kernel_interval(int n, float d, float *x, float *y, float *z, float *out)
{
    const float r2 = (d * d) / 4.0f;
    const int step = blockDim.x * gridDim.x;

    float vx = 0.0f;
    float vy = 0.0f;

    float prev = 0;
    float max_interval = 0;

    for (int i = blockIdx.x * blockDim.x + threadIdx.x; i < n; i += step)
    {
        prev = z[i];
        max_interval = 0;
        for (int j = 0; j < n; j++)
        {
            vx = x[j] - x[i];
            vy = y[j] - y[i];

            if (vx * vx + vy * vy <= r2 && z[j] < prev)
            {
                if (prev - z[j] > max_interval)
                {
                    max_interval = prev - z[j];
                }
                prev = z[j];
            }
        }
        out[i] = max_interval;
    }
}

__global__ void kernel_min(int n, float d, float *x, float *y, float *z, float *out)
{
    const float r2 = (d * d) / 4.0f;
    const int step = blockDim.x * gridDim.x;

    float vx = 0.0f;
    float vy = 0.0f;
    float m = 0.0f;
    for (int i = blockIdx.x * blockDim.x + threadIdx.x; i < n; i += step)
    {
        m = z[i];
        for (int j = 0; j < n; j++)
        {
            vx = x[j] - x[i];
            vy = y[j] - y[i];

            if (vx * vx + vy * vy <= r2 && z[j] < m)
            {
                m = z[j];
            }
        }
        out[i] = m;
    }
}
''')
kernel_clusterization = mod.get_function('kernel_clusterization')
kernel_neighbors = mod.get_function('kernel_neighbors')
kernel_lmf = mod.get_function('kernel_lmf')
kernel_interval = mod.get_function('kernel_interval')
kernel_min = mod.get_function('kernel_min')


def las_to_points(las: LasData):
    x = np.array(las.x, dtype=np.float64)
    y = np.array(las.y, dtype=np.float64)
    z = np.array(las.z, dtype=np.float64)
    return np.vstack((x, y, z), dtype=np.float64)


def write_points_to_las(points, out_file_path: str, point_format, file_version: str):
    new_las = laspy.create(
        point_format=point_format,
        file_version=file_version
    )
    new_las.x = points[0]
    new_las.y = points[1]
    new_las.z = points[2]

    new_las.write(out_file_path)


def delete_ground_noise_and_grass(las_file_path: str,
                                  min_height: float = 0,
                                  indent: int = 2
                                  ) -> (ndarray[Any, dtype[Any]], laspy.PointFormat, laspy.header.Version):
    """
    Функция удаляет землю и шум. Удаляет траву ниже указанной высоты, если кол-во точек травы
    составляет более 5 % от общего кол-ва точек.
    Используется R

    :param las_file_path: путь к las файлу
    :param min_height: минимальная высота травы
    :param indent: кол-во пробелов(для вывода)
    :return:
    """
    importr('lidR')
    importr('RCSF')

    robjects.r(
        ''' 
            preprocess <- function(lasfile, save = F, outfile = "") { 
                las <- readLAS(lasfile)
                las <- classify_noise(las, sor(10, 0.99, TRUE))
                las <- filter_poi(las, Classification != LASNOISE)
                las <- classify_ground(las, algorithm = csf())
                las <- normalize_height(las, knnidw())
                las <- filter_poi(las, Classification != LASGROUND)
                if (save) {
                    writeLAS(las, outfile, index = FALSE)
                }
                return(las)
            }  
        '''
    )

    preprocess = robjects.r['preprocess']
    las = preprocess(las_file_path)
    data = las.slots['data']

    x = np.array(data.rx2('X'), dtype=np.float64)
    y = np.array(data.rx2('Y'), dtype=np.float64)
    z = np.array(data.rx2('Z'), dtype=np.float64)

    points = np.vstack((x, y, z), dtype=np.float64).transpose()

    las = laspy.read(las_file_path)

    print(f'{" " * indent}Общее количество точек = {len(las.x)}')
    print(f'{" " * indent}Количество точек после фильтрации земли и шума = {len(x)}')
    grass_points = points[points[:, 2] < min_height]
    if len(grass_points) / len(points) > 0.05:
        points = points[points[:, 2] >= min_height]
    print(f'{" " * indent}Количество точек после фильтрации по высоте = {len(points)}')

    points = points.transpose()

    return points, las.header.point_format, las.header.version


def filter_interval(points: ndarray[Any, dtype[Any]],
                    d: float,
                    q: float,
                    blocks: int,
                    threads_per_block: int,
                    indent: int = 2
                    ) -> ndarray[Any, dtype[Any]]:
    """
    Функция фильтрации точек по размеру максимального разрыва между двумя соседними точками в столбце

    :param points: массив точек
    :param d: диаметр поиска
    :param q: квантиль [0:1]
    :param blocks: кол-во блоков
    :param threads_per_block: кол-во потоков на блок
    :param indent: кол-во пробелов(для вывода)
    :return: отфильтрованный массив точек
    """
    points = points.transpose()
    points = points[points[:, 2].argsort()[::-1]]
    points = points.transpose()

    x = points[0].copy()
    y = points[1].copy()
    z = points[2].copy()

    x_min = x.min()
    y_min = y.min()

    x -= x_min
    y -= y_min

    x = x.astype(np.float32)
    y = y.astype(np.float32)
    z = z.astype(np.float32)

    n = len(x)
    print(f'{" " * indent}Количество точек до фильтрации - {n}')

    intervals = np.zeros(n, dtype=np.float32)
    kernel_interval(
        np.int32(n), np.float32(d),
        drv.In(x), drv.In(y), drv.In(z),
        drv.Out(intervals),
        block=(threads_per_block, 1, 1), grid=(blocks, 1)
    )

    filtered_intervals = intervals[0.001 < intervals]
    filtered_intervals = filtered_intervals[filtered_intervals < z.max() / 2]

    value_q = np.quantile(filtered_intervals, q)
    b = []

    for i in range(0, n):
        b.append(intervals[i] < value_q)

    points = points.transpose()
    points = points[b]
    print(f'{" " * indent}Количество точек после фильтрации - {len(points)}')
    return points.transpose()


def clusterization(points: ndarray[Any, dtype[Any]],
                   k: int,
                   d: float,
                   blocks: int,
                   threads_per_block: int,
                   indent: int = 2
                   ) -> ndarray[Any, dtype[Any]]:
    """
    Функция кластеризации массива точек, основанная на сдвиге каждой точки к центру масс

    :param points: массив точек
    :param k: кол-во шагов кластеризации
    :param d: диаметр(в метрах)
    :param blocks: кол-во блоков
    :param threads_per_block: кол-во потоков на блок
    :param indent: кол-во пробелов(для вывода)
    :return: кластеризованный массив точек
    """
    x = points[0].copy()
    y = points[1].copy()
    z = points[2].copy()

    x_min = x.min()
    y_min = y.min()

    x -= x_min
    y -= y_min

    x = x.astype(np.float32)
    y = y.astype(np.float32)

    n = len(x)

    for i in range(1, k + 1):
        start = time()

        cur_x = x.copy()
        cur_y = y.copy()

        kernel_clusterization(
            np.int32(n), np.float32(d),
            drv.In(cur_x), drv.In(cur_y),
            drv.Out(x), drv.Out(y),
            block=(threads_per_block, 1, 1), grid=(blocks, 1)
        )

        print(f'{' ' * indent}{i}: {time() - start}')

    x = x.astype(np.float64)
    y = y.astype(np.float64)
    x += x_min
    y += y_min

    return np.vstack((x, y, z), dtype=np.float64)


def lmf(points: ndarray[Any, dtype[Any]],
        d: float,
        q: float,
        blocks: int,
        threads_per_block: int,
        indent: int = 2) -> ndarray[Any, dtype[Any]]:
    """
    Функция поиска локальных максимумов, с последующей фильтрацией по минимальной высоте столбца

    :param points: массив точек
    :param d: диаметр(в метрах)
    :param q: квантиль [0:1]
    :param blocks: кол-во блоков
    :param threads_per_block: кол-во потоков на блок
    :param indent: кол-во пробелов(для вывода)
    :return:
    """
    points = points.transpose()
    points = points[points[:, 2].argsort()[::-1]]
    points = points.transpose()

    x = points[0].copy()
    y = points[1].copy()
    z = points[2].copy()

    x_min = x.min()
    y_min = y.min()

    x -= x_min
    y -= y_min

    x = x.astype(np.float32)
    y = y.astype(np.float32)
    z = z.astype(np.float32)

    points = points.transpose()

    n = len(x)
    out = np.ones(n, dtype=bool)
    kernel_lmf(
        np.int32(n), np.float32(d),
        drv.In(x), drv.In(y), drv.In(z),
        drv.InOut(out),
        block=(threads_per_block, 1, 1), grid=(blocks, 1)
    )

    vec_min = np.zeros(n, dtype=np.float32)
    kernel_min(
        np.int32(n), np.float32(d),
        drv.In(x), drv.In(y), drv.In(z),
        drv.Out(vec_min),
        block=(threads_per_block, 1, 1), grid=(blocks, 1)
    )

    vec_m = np.zeros(n, dtype=np.float32)
    kernel_neighbors(
        np.int32(n), np.float32(d),
        drv.In(x), drv.In(y), drv.In(z),
        drv.Out(vec_m),
        block=(threads_per_block, 1, 1), grid=(blocks, 1)
    )

    res = []
    mins = []

    for i in range(0, n):
        if out[i] and 0.1 < vec_min[i] < z.max() / 2:
            mins.append(vec_min[i])

    mins = np.array(mins)
    zs = np.quantile(mins, q)
    all_tops = []
    for i in range(0, n):
        if points[i][2] >= 8:
            all_tops.append(points[i])
            if out[i] and vec_min[i] < zs and vec_m[i] > n * np.float32(0.0001):
                res.append(points[i])

    print(f'{" " * indent}Количество найденных деревьев = {len(res)}')

    points = np.array(res)
    points = points.transpose()

    return points


# Функции для назначения пород

def calculate_distance(point1: tuple, point2: tuple) -> float:
    """
    Вычисляет евклидово расстояние между двумя точками.

    :param point1: Кортеж (x1, y1, z1) - координаты первой точки
    :param point2: Кортеж (x2, y2, z2) - координаты второй точки
    :return: Расстояние между точками
    """
    return np.sqrt(((point2[0] - point1[0]) ** 2) + ((point2[1] - point1[1]) ** 2))


def enrich(points: ndarray[Any, dtype[Any]], shapefile_path: str) -> pd.DataFrame:
    """
    Обогащает точки вершин породой по ближайшему региону к каждой точке

    :param points: массив точек
    :param shapefile_path: путь до shp файла
    :return: DataFrame с колонками x, y, h, s(sample)
    """

    old = points
    points = []
    for i in range(0, len(old[0])):
        points.append((old[0][i], old[1][i], old[2][i]))

    gdf = gpd.read_file(shapefile_path)

    # Вычисляем центры полигонов
    gdf['centroid'] = gdf.geometry.centroid
    centroids = np.array(list(zip(gdf['centroid'].x, gdf['centroid'].y, np.zeros(len(gdf)))))  # Добавляем Z = 0

    results = []
    polygons_id = []
    # Для каждой точки находим ближайший центр полигона
    for point in points:
        point_index = 0
        point_geom = Point(point[0], point[1])
        nearest_region_id = -1
        min_distance = float('inf')

        # Проверяем, находится ли точка в каком-то из регионов
        for i, centroid in enumerate(centroids):
            if gdf.geometry.contains(point_geom).any():
                distance = calculate_distance(centroid, point)
                if distance < min_distance:
                    min_distance = distance
                    nearest_region_id = gdf.index[i]

        if nearest_region_id != -1 and nearest_region_id not in polygons_id:
            polygons_id.append(nearest_region_id)
            results.append({
                'x': point[0],
                'y': point[1],
                'h': point[2],
                's': gdf.s[nearest_region_id],
            })
        else:
            results.append({
                'x': point[0],
                'y': point[1],
                'h': point[2],
                's': -1,
            })
        point_index += 1
    results_df = pd.DataFrame(results)
    return results_df


def main():
    k = 15
    min_height = 3
    d_clusterization = np.float32(2.5)
    threads_per_block = 1024
    blocks = 2048
    d_lmf = np.float32(0.6)
    q = 0.95

    las_file_path = argv[1]
    shp_file_path = argv[2]
    out_res_file_path = argv[3]

    points, point_format, file_version = delete_ground_noise_and_grass(
        las_file_path,
        min_height
    )

    points = clusterization(
        points,
        k,
        d_clusterization,
        blocks,
        threads_per_block
    )

    points = filter_interval(
        points,
        d_lmf,
        q,
        blocks,
        threads_per_block
    )

    points = lmf(
        points,
        d_lmf,
        q,
        blocks,
        threads_per_block
    )

    data = enrich(points, shp_file_path)
    data.to_csv(out_res_file_path)


if __name__ == '__main__':
    main()

