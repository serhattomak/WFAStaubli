using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

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
                if (originalImage.Width == 0 || originalImage.Height == 0)
                {
                    MessageBox.Show("Loaded image is empty.");
                    return;
                }

                Bitmap convertedImage = ConvertToBlackAndWhite(originalImage);
                pcbConverted.Image = convertedImage;
            }
        }
        private void btnCommand_Click(object sender, EventArgs e)
        {
            if (pcbConverted.Image != null)
            {
                Bitmap convertedImage = new Bitmap(pcbConverted.Image);

                if (convertedImage.Width == 0 || convertedImage.Height == 0)
                {
                    MessageBox.Show("Converted image is empty.");
                    return;
                }

                List<Point> pathPoints = CreatePathFromImage(convertedImage);

                // Deconstruct the tuple returned by IdentifyLinesAndCurves
                var (lines, curves) = IdentifyLinesAndCurves(pathPoints);

                List<string> commands = GenerateRobotCommands(lines, curves);

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Text Files (*.txt)|*.txt|All files (*.*)|*.*",
                    Title = "Save Robot Commands"
                };

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    using (StreamWriter sw = new StreamWriter(saveFileDialog.FileName))
                    {
                        foreach (string command in commands)
                        {
                            sw.WriteLine(command);
                        }
                    }
                }
            }
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
            const int proximityThreshold = 5; // Points within this distance will be considered in the same cluster
            bool[,] visited = new bool[image.Width, image.Height];

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (visited[x, y])
                        continue;

                    Color pixelColor = image.GetPixel(x, y);
                    if (pixelColor.R == 0 && pixelColor.G == 0 && pixelColor.B == 0)
                    {
                        // Found a black pixel
                        bool isIsolated = true;
                        for (int nx = Math.Max(0, x - proximityThreshold); nx < Math.Min(image.Width, x + proximityThreshold); nx++)
                        {
                            for (int ny = Math.Max(0, y - proximityThreshold); ny < Math.Min(image.Height, y + proximityThreshold); ny++)
                            {
                                if (nx == x && ny == y) continue; // skip the current pixel

                                if (image.GetPixel(nx, ny).R == 0 && image.GetPixel(nx, ny).G == 0 && image.GetPixel(nx, ny).B == 0)
                                {
                                    visited[nx, ny] = true; // Mark nearby black pixels as visited
                                    isIsolated = false;
                                }
                            }
                        }

                        if (!isIsolated)
                        {
                            path.Add(new Point(x, y));
                        }
                    }
                }
            }

            return path;
        }


        #endregion

        #region Çizgi ve Eğri Tanımı

        private (List<LineSegment2D>, List<VectorOfPoint>) IdentifyLinesAndCurves(List<Point> path)
        {
            List<LineSegment2D> lines = new List<LineSegment2D>();
            List<VectorOfPoint> curves = new List<VectorOfPoint>();
            var groupedPoints = GroupAdjacentPoints(path);

            foreach (var group in groupedPoints)
            {
                if (IsLine(group))
                {
                    // Assuming the line can be represented by its start and end points
                    lines.Add(new LineSegment2D(group.First(), group.Last()));
                }
                else
                {
                    // For curves, create a VectorOfPoint from the group
                    VectorOfPoint curve = new VectorOfPoint(group.ToArray());
                    curves.Add(curve);
                }
            }

            return (lines, curves);
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

        #region Çizgi ve Eğri Tanımı (EmguCV)
        // Çizgi Tespiti
        private List<LineSegment2D> DetectLines(Bitmap image)
        {
            List<LineSegment2D> lines = new List<LineSegment2D>();

            if (image == null || image.Width == 0 || image.Height == 0)
            {
                throw new InvalidOperationException("Image is not loaded correctly for line detection.");
            }

            // Convert the Bitmap to a Mat
            using (Mat imageMat = image.ToMat())
            {
                CvInvoke.CvtColor(imageMat, imageMat, ColorConversion.Bgr2Gray);
                using (UMat cannyEdges = new UMat())
                {
                    CvInvoke.Canny(imageMat, cannyEdges, 180, 120);
                    LineSegment2D[] detectedLines = CvInvoke.HoughLinesP(
                        cannyEdges,
                        1, // Distance resolution in pixels
                        Math.PI / 45.0, // Angle resolution in radians
                        20, // Threshold
                        30, // Minimum width of a line
                        10); // Gap between lines

                    lines.AddRange(detectedLines);
                }
            }

            return lines;
        }




        // Eğri Tespiti
        private List<VectorOfPoint> DetectCurves(Bitmap image)
        {
            List<VectorOfPoint> curves = new List<VectorOfPoint>();

            if (image == null || image.Width == 0 || image.Height == 0)
            {
                throw new InvalidOperationException("Image is not loaded correctly for curve detection.");
            }

            // Convert the Bitmap to a Mat
            using (Mat imageMat = image.ToMat())
            {
                CvInvoke.CvtColor(imageMat, imageMat, ColorConversion.Bgr2Gray);

                // Use Canny edge detection
                using (Mat cannyEdges = new Mat())
                {
                    CvInvoke.Canny(imageMat, cannyEdges, 180, 120);

                    // Find contours
                    using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
                    {
                        CvInvoke.FindContours(cannyEdges, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);
                        for (int i = 0; i < contours.Size; i++)
                        {
                            using (VectorOfPoint contour = contours[i])
                            {
                                VectorOfPoint approxContour = new VectorOfPoint();
                                CvInvoke.ApproxPolyDP(contour, approxContour, CvInvoke.ArcLength(contour, true) * 0.015, true);
                                if (CvInvoke.ContourArea(approxContour, false) > 10) // Filter small contours
                                {
                                    curves.Add(approxContour);
                                }
                            }
                        }
                    }
                }
            }

            return curves;
        }

        #endregion

        #region Robot Komutlarını Oluşturma
        private List<string> GenerateRobotCommands(List<LineSegment2D> lines, List<VectorOfPoint> curves)
        {
            List<string> commands = new List<string>();

            // Process lines
            foreach (var line in lines)
            {
                // MoveJ to the start of the line
                commands.Add($"moveJ({line.P1.X}, {line.P1.Y})");
                // MoveL to the end of the line
                commands.Add($"moveL({line.P2.X}, {line.P2.Y})");
            }

            // Process curves
            // For simplicity, assuming a curve is a series of MoveL commands
            foreach (var curve in curves)
            {
                // MoveJ to the start of the curve
                Point startPoint = curve[0];
                commands.Add($"moveJ({startPoint.X}, {startPoint.Y})");

                // MoveL through the curve
                for (int i = 1; i < curve.Size; i++)
                {
                    Point point = curve[i];
                    commands.Add($"moveL({point.X}, {point.Y})");
                }
            }

            return commands;
        }
        #endregion
    }
}
