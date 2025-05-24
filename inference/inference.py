import os
import sys
import rasterio
import cv2
import geopandas as gpd
from shapely.geometry import Polygon
from ultralytics import YOLO
from mappings import class_names, class_codes



def main(model,input_path : str, output_path, labels : bool):
    with rasterio.open(input_path) as src:
        transform = src.transform
        crs = src.crs
        h, w, c = cv2.imread(input_path).shape
        results = model.predict(input_path, imgsz=[h, w],
                                augment=False,
                                conf=0.31,
                                agnostic_nms=True,
                                save=True,
                                show_labels= False,
                                project="model_output",
                                #name=img,
                                exist_ok=True
                                )
        predictions = results[0].boxes
        polygons = []
        codes = []
        labels = []  # Массив для меток пород

        for i in range(len(predictions.xywh)):
            # Составляем многоугольники из координат (предполагается, что это прямоугольники)
            x, y, w, h = predictions.xywh[i].tolist()

            # Преобразуем пиксельные координаты в мировые с помощью трансформации
            x_min, y_max = transform * (x - w / 2, y - h / 2)  # Верхний левый угол
            x_max, y_min = transform * (x + w / 2, y + h / 2)  # Нижний правый угол

            # Создаем многоугольник из мировых координат
            poly = Polygon([
                (x_min, y_max),
                (x_max, y_max),
                (x_max, y_min),
                (x_min, y_min)
            ])
            polygons.append(poly)

            # Получаем код класса и соответствующую метку
            class_id = predictions.cls[i].item()
            class_label = class_names.get(class_id, 'Неизвестно')  # Получаем метку породы
            class_code = class_codes.get(class_id, -1)  # Получаем код породы (если он существует)
            labels.append(class_label)  # Добавляем метку породы
            codes.append(class_code)  # Сохраняем код породы

        # Создаем GeoDataFrame с указанием столбца 's'
        df = gpd.GeoDataFrame({
            'geometry': polygons,
            's': codes  # Метка породы
        }, crs=crs)  # Используем CRS из изображения

        df.to_file(output_path, driver='ESRI Shapefile')
        #results[0].save(f"{output_path[:-4]}.jpg")


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Использование: python script.py <путь_к_входной_директории/файлу> <путь_к_директории_результатов>")


    input_path = sys.argv[1]
    output_folder = sys.argv[2]
    labels = False


    if not os.path.exists(output_folder):
        os.makedirs(output_folder)

    try:
        script_dir = os.path.dirname(os.path.abspath(__file__))

        # Формирование пути к файлу модели
        model_path = os.path.join(script_dir, 'best.pt')

        # Загрузка модели
        model = YOLO(model_path)

        if os.path.isfile(input_path):  # Если указан файл
            output_path = os.path.join(output_folder, 'output.shp')
            main(model, input_path, output_path, labels)
        elif os.path.isdir(input_path):  # Если указана папка
            files_processed = 0
            for idx, filename in enumerate(os.listdir(input_path)):
                if filename.lower().endswith('.tif'):  # Проверяем, что это изображение .tif
                    file_path = os.path.join(input_path, filename)
                    output_path = os.path.join(output_folder, f'output_{idx + 1}.shp')  # Сохраняем как shp

                    print(f"Обработка файла: {filename}")
                    # Обрабатываем файл
                    main(model, file_path, output_path, labels)
                    files_processed += 1

            if files_processed == 0:
                print(f"В папке {input_path} не найдено файлов .tif для обработки.")
            else:
                print(f"Обработано {files_processed} файлов.")
        else:
            print(f"Ошибка: Указанный путь {input_path} не является файлом или папкой.")
            sys.exit(1)

    except Exception as e:
        print(f"Произошла ошибка: {e}")