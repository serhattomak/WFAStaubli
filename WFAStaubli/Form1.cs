using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
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
                pcbOriginal.SizeMode = PictureBoxSizeMode.StretchImage;
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
                pcbConverted.SizeMode = PictureBoxSizeMode.StretchImage;
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

                // Step 1: Initial detection of lines and curves
                var initialLines = DetectLines(convertedImage);
                var initialCurves = DetectCurves(convertedImage);

                // Step 2: Create a path from the image for further refinement
                List<Point> pathPoints = CreatePathFromImage(convertedImage);

                // Use the path points to identify refined lines and curves
                var (refinedLines, refinedCurves) = IdentifyLinesAndCurves(pathPoints);

                // Optional: Combine initial and refined detections or choose one over the other
                // For demonstration, let's proceed with the refined detections
                // You can modify this logic based on your application's needs
                List<string> commands = GenerateRobotCommands(refinedLines, refinedCurves);

                // Step 3: Prompt the user to save the commands to a file
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
                    lines.Add(new LineSegment2D(group.First(), group.Last()));
                }
                else
                {
                    // Treat every non-line group as a curve for now
                    curves.Add(new VectorOfPoint(group.ToArray()));
                }
            }

            UpdateDebugInfo($"Lines: {lines.Count}, Curves: {curves.Count}");

            return (lines, curves);
        }


        private double GetDistance(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }



        private List<List<Point>> GroupAdjacentPoints(List<Point> path)
        {
            List<List<Point>> groups = new List<List<Point>>();
            List<Point> currentGroup = new List<Point>();
            // Define the proximity threshold - how close points should be to each other to be considered in the same group
            int proximityThreshold = 10;  // You may adjust this value based on your image's resolution and scale

            foreach (Point point in path)
            {
                if (currentGroup.Count == 0)
                {
                    currentGroup.Add(point);
                }
                else
                {
                    Point lastPoint = currentGroup.Last();
                    // Adjust this distance as needed
                    if (Math.Abs(point.X - lastPoint.X) <= proximityThreshold && Math.Abs(point.Y - lastPoint.Y) <= proximityThreshold)
                    {
                        currentGroup.Add(point);
                    }
                    else
                    {
                        if (currentGroup.Count > 0)
                        {
                            groups.Add(new List<Point>(currentGroup));
                            currentGroup.Clear();
                        }
                        currentGroup.Add(point);
                    }
                }
            }

            if (currentGroup.Count > 0)
            {
                groups.Add(currentGroup);
            }

            // Debugging: Output the group sizes to understand the grouping
            foreach (var group in groups)
            {
                Debug.WriteLine($"Group size: {group.Count}");
            }

            return groups;
        }


        //private bool IsLine(List<Point> points)
        //{
        //    // Consider adjusting the slope tolerance or using a different line detection logic
        //    const double slopeTolerance = 0.2; // Adjusted tolerance

        //    if (points.Count < 2)
        //    {
        //        return false;
        //    }

        //    // You could also consider using a more sophisticated line fitting approach here
        //    Point startPoint = points.First();
        //    Point endPoint = points.Last();
        //    double expectedSlope = Math.Abs(endPoint.Y - startPoint.Y) / (double)(endPoint.X - startPoint.X + 0.0001);

        //    foreach (Point point in points)
        //    {
        //        double actualSlope = Math.Abs(point.Y - startPoint.Y) / (double)(point.X - startPoint.X + 0.0001);
        //        if (Math.Abs(actualSlope - expectedSlope) > slopeTolerance)
        //        {
        //            return false;
        //        }
        //    }

        //    return true;
        //}
        private bool IsLine(List<Point> points)
        {
            if (points.Count < 2)
                return false;

            Point startPoint = points.First();
            Point endPoint = points.Last();

            // Define a tolerance for how far points can be from the line
            double tolerance = 5.0; // This can be adjusted based on your needs

            for (int i = 1; i < points.Count - 1; i++)
            {
                if (DistanceFromPointToLine(points[i], startPoint, endPoint) > tolerance)
                {
                    return false;
                }
            }

            return true;
        }


        private double CalculateAngle(Point a, Point b, Point c)
        {
            var ab = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
            var bc = Math.Sqrt(Math.Pow(b.X - c.X, 2) + Math.Pow(b.Y - c.Y, 2));
            var ac = Math.Sqrt(Math.Pow(c.X - a.X, 2) + Math.Pow(c.Y - a.Y, 2));
            return Math.Acos((bc * bc + ab * ab - ac * ac) / (2 * bc * ab)) * (180 / Math.PI);
        }


        private double DistanceFromPointToLine(Point p, Point a, Point b)
        {
            double normalLength = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            return Math.Abs((p.X - a.X) * (b.Y - a.Y) - (p.Y - a.Y) * (b.X - a.X)) / normalLength;
        }


        private bool IsPointOnLine(Point a, Point b, Point point)
        {
            // Basic collinearity check (within a tolerance to account for integer rounding)
            int dx = b.X - a.X;
            int dy = b.Y - a.Y;
            int crossProduct = (point.Y - a.Y) * dx - (point.X - a.X) * dy;
            return Math.Abs(crossProduct) < 1000;  // Adjust tolerance as needed
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

                    // Filter out short lines
                    const double minLineLength = 20.0; // Define a minimum line length threshold
                    foreach (var line in detectedLines)
                    {
                        if (line.Length >= minLineLength)
                        {
                            lines.Add(line);
                        }
                    }
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

        private void UpdateDebugInfo(string text)
        {
            // Assuming you have a label named debugLabel for debugging output
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => debugLabel.Text = text));
            }
            else
            {
                debugLabel.Text = text;
            }
        }
    }
}
