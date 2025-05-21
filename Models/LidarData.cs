using System;
using System.Collections.Generic;

namespace IAFTS.Models
{
    public class LidarData
    {
        public string? LasFilePath { get; set; }    // Путь к файлу LAS
        public string? TiffFilePath { get; set; }   // Путь к файлу TIFF
        public string? OutputPath { get; set; }     // Путь для сохранения результатов
        public List<Tree>? Trees { get; set; }      // Список обнаруженных деревьев

        public LidarData()
        {
            Trees = new List<Tree>();
        }
    }

    public class Tree
    {
        public double X { get; set; }  // EPSG координаты
        public double Y { get; set; }
        public double Height { get; set; }      // Высота дерева
        public string? Species { get; set; }     // Порода дерева
    }
}