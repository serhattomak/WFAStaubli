using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WFAStaubli
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        #region Butonlar
        private void btnUpload_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png",
                Title = "Select an Image"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // Load and display the image in the PictureBox
                pcbOriginal.Image = new Bitmap(openFileDialog.FileName);
            }
        }
        private void btnConvert_Click(object sender, EventArgs e)
        {
            if (pcbOriginal.Image != null)
            {
                Bitmap originalImage = new Bitmap(pcbOriginal.Image);
                Bitmap convertedImage = ConvertToBlackAndWhite(originalImage);
                pcbConverted.Image = convertedImage;

                List<Point> path = CreatePathFromImage(convertedImage);
            }
        }
        private void btnCommand_Click(object sender, EventArgs e)
        {

        }
        #endregion

        #region Siyah - Beyaz Dönüşüm

        // Siyah - Beyaz Dönüşüm Metodu
        private Bitmap ConvertToBlackAndWhite(Bitmap originalImage)
        {
            Bitmap blackAndWhiteImage = new Bitmap(originalImage.Width, originalImage.Height);

            for (int i = 0; i < originalImage.Width; i++)
            {
                for (int j = 0; j < originalImage.Height; j++)
                {
                    Color originalColor = originalImage.GetPixel(i, j);
                    // Calculate the grayscale value
                    int grayScale = (int)((originalColor.R * 0.3) + (originalColor.G * 0.59) + (originalColor.B * 0.11));
                    // Apply the threshold
                    Color newColor = grayScale < 128 ? Color.FromArgb(255, 0, 0, 0) : Color.FromArgb(255, 255, 255, 255);
                    blackAndWhiteImage.SetPixel(i, j, newColor);
                }
            }
            return blackAndWhiteImage;
        }

        #endregion

        #region Yol Oluşturma

        // Görsel Üzerinden Yol Oluşturma Metodu

        private List<Point> CreatePathFromImage(Bitmap image)
        {
            List<Point> path = new List<Point>();

            // Scan the image row by row
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Color pixelColor = image.GetPixel(x, y);
                    // Note: This could be adjusted depending on how the image is processed
                    if (pixelColor.R == 0 && pixelColor.G == 0 && pixelColor.B == 0)
                    {
                        path.Add(new Point(x, y));
                    }
                }
            }

            return path;
        }

        #endregion

        #region Çizgi ve Eğri Tanımı

        private void IdentifyLinesAndCurves(List<Point> path)
        {
            var groupedPoints = GroupAdjacentPoints(path);
            List<List<Point>> lines = new List<List<Point>>();
            List<List<Point>> curves = new List<List<Point>>();

            foreach (var group in groupedPoints)
            {
                if (IsLine(group))
                {
                    lines.Add(group);
                }
                else
                {
                    curves.Add(group);
                }
            }

            // At this point, 'lines' contains groups of points forming lines, and 'curves' contains the rest.
            // You can further process these to generate drawing commands or visualize them.
        }

        private List<List<Point>> GroupAdjacentPoints(List<Point> path)
        {
            List<List<Point>> groups = new List<List<Point>>();
            List<Point> currentGroup = new List<Point>();

            foreach (Point point in path)
            {
                if (currentGroup.Count == 0)
                {
                    currentGroup.Add(point);
                }
                else
                {
                    Point lastPoint = currentGroup[currentGroup.Count - 1];
                    // Check if the current point is adjacent to the last point in the current group
                    if (Math.Abs(point.X - lastPoint.X) <= 1 && Math.Abs(point.Y - lastPoint.Y) <= 1)
                    {
                        currentGroup.Add(point);
                    }
                    else
                    {
                        groups.Add(new List<Point>(currentGroup));
                        currentGroup.Clear();
                        currentGroup.Add(point);
                    }
                }
            }

            if (currentGroup.Count > 0)
            {
                groups.Add(currentGroup);
            }

            return groups;
        }

        private bool IsLine(List<Point> points)
        {
            if (points.Count < 2)
            {
                return false;
            }

            const double slopeTolerance = 0.1; // Adjust tolerance as needed
            double expectedSlope = (double)(points[1].Y - points[0].Y) / (points[1].X - points[0].X);

            for (int i = 1; i < points.Count - 1; i++)
            {
                double slope = (double)(points[i + 1].Y - points[i].Y) / (points[i + 1].X - points[i].X);
                if (Math.Abs(slope - expectedSlope) > slopeTolerance)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
