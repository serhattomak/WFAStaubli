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
using System.Xml.Linq;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using static System.Windows.Forms.LinkLabel;

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
            ResetFirstPoint();
            if (pcbConverted.Image != null)
            {
                Bitmap convertedImage = pcbConverted.Image as Bitmap; // Safe cast

                if (convertedImage == null || convertedImage.Width == 0 || convertedImage.Height == 0)
                {
                    MessageBox.Show("Converted image is empty or not a Bitmap.");
                    return;
                }

                double[] rotation = DetectOrientation(convertedImage); // Now correctly passing a Bitmap

                var initialLines = DetectLines(convertedImage);
                var initialCurves = DetectCurves(convertedImage);

                List<Point> pathPoints = CreatePathFromImage(convertedImage);
                List<Point> sortedPoints = SortPathPoints(pathPoints); // Sort points for logical movement

                var (refinedLines, refinedCurves) = IdentifyLinesAndCurves(sortedPoints); // Use sorted points here

                // Calculate bounding box and translation
                var (minX, minY, _, _) = CalculateBoundingBox(sortedPoints);
                var translatedPoints = TranslatePoints(sortedPoints, -minX, -minY);

                var (translatedLines, translatedCurves) = IdentifyLinesAndCurves(translatedPoints);

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Text Files (*.txt)|*.txt|PGX Files (*.pgx)|*.pgx",
                    Title = "Save Robot Commands"
                };

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    List<string> commands = GenerateRobotCommands(translatedLines, translatedCurves); // Assuming rotation is used here
                    if (saveFileDialog.FileName.EndsWith(".txt"))
                    {
                        SaveCommandsAsText(saveFileDialog.FileName, commands);
                    }
                    else if (saveFileDialog.FileName.EndsWith(".pgx"))
                    {
                        SaveCommandsAsXml(saveFileDialog.FileName, commands);
                    }
                }
            }
        }

        private void btnDraw_Click(object sender, EventArgs e)
        {
            if (pcbConverted.Image != null)
            {
                Bitmap convertedImage = pcbConverted.Image as Bitmap;

                if (convertedImage == null || convertedImage.Width == 0 || convertedImage.Height == 0)
                {
                    MessageBox.Show("Converted image is empty or not a Bitmap.");
                    return;
                }

                List<Point> pathPoints = CreatePathFromImage(convertedImage);
                List<Point> sortedPoints = SortPathPoints(pathPoints);

                var (refinedLines, refinedCurves) = IdentifyLinesAndCurves(sortedPoints);

                // Calculate bounding box and translation
                var (minX, minY, _, _) = CalculateBoundingBox(sortedPoints);
                var translatedPoints = TranslatePoints(sortedPoints, -minX, -minY);

                var (translatedLines, translatedCurves) = IdentifyLinesAndCurves(translatedPoints);

                List<string> commands = GenerateRobotCommands(translatedLines, translatedCurves);

                DrawRobotCommands(commands);
            }
        }
        #endregion

        #region Siyah - Beyaz Dönüşüm

        // Siyah - Beyaz Dönüşüm Metodu
        private Bitmap ConvertToBlackAndWhite(Bitmap originalImage)
        {
            Image<Gray, byte> grayImage = new Image<Gray, byte>(originalImage.Width, originalImage.Height);

            for (int i = 0; i < originalImage.Width; i++)
            {
                for (int j = 0; j < originalImage.Height; j++)
                {
                    Color originalColor = originalImage.GetPixel(i, j);
                    if (originalColor.A < 128)
                    {
                        grayImage.Data[j, i, 0] = 255;
                    }
                    else
                    {
                        int grayScale = (int)((originalColor.R * 0.3) + (originalColor.G * 0.59) + (originalColor.B * 0.11));
                        grayImage.Data[j, i, 0] = grayScale < 128 ? (byte)0 : (byte)255;
                    }
                }
            }

            int erosionSize = 2;
            Mat elementErode = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(erosionSize, erosionSize), new Point(-1, -1));
            CvInvoke.Erode(grayImage, grayImage, elementErode, new Point(-1, -1), 1, BorderType.Reflect, default(MCvScalar));

            int dilationSize = 2;
            Mat elementDilate = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(dilationSize, dilationSize), new Point(-1, -1));
            CvInvoke.Dilate(grayImage, grayImage, elementDilate, new Point(-1, -1), 1, BorderType.Reflect, default(MCvScalar));

            return grayImage.ToBitmap();
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

        private double DistanceFromPointToLine(Point p, Point a, Point b)
        {
            double normalLength = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            return Math.Abs((p.X - a.X) * (b.Y - a.Y) - (p.Y - a.Y) * (b.X - a.X)) / normalLength;
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

        #region Orientation Detection

        private double[] DetectOrientation(Bitmap image)
        {
            double rx = 0; // Placeholder for rotation around the X-axis
            double ry = 0; // Placeholder for rotation around the Y-axis
            double rz = 0; // Placeholder for rotation around the Z-axis

            return new double[] { rx, ry, rz };
        }

        #endregion

        #region Robot Komutlarını Oluşturma

        private List<string> GenerateRobotCommands(List<LineSegment2D> lines, List<VectorOfPoint> curves)
        {
            List<string> commands = new List<string>();

            double safeHeight = 0;
            double drawHeight = 20;
            bool isFirstCommand = true;
            bool inDrawing = false;

            double[] defaultOrientation = DetectOrientation((Bitmap)pcbConverted.Image);

            commands.Add(FormatCommand("movej", new Point(0, 0), defaultOrientation, safeHeight));

            Point lastPoint = new Point();
            double lastZ = safeHeight;

            // Process lines
            foreach (var line in lines)
            {
                var (robotX1, robotY1) = DefinePoint(line.P1.X, line.P1.Y, scaleFactor, marginX, marginY);
                var (robotX2, robotY2) = DefinePoint(line.P2.X, line.P2.Y, scaleFactor, marginX, marginY);

                Point startPoint = new Point((int)robotX1, (int)robotY1);
                Point endPoint = new Point((int)robotX2, (int)robotY2);

                double[] startOrientation = CalculateOrientationForPoint(startPoint);
                double[] endOrientation = CalculateOrientationForPoint(endPoint);

                if (!isFirstCommand && lastZ == safeHeight && !inDrawing)
                {
                    commands.Add("waitEndMove()");
                }

                double firstPointZ = isFirstCommand ? safeHeight : drawHeight;
                if (!isFirstCommand || firstPointZ == safeHeight)
                {
                    commands.Add(FormatCommand("movej", startPoint, startOrientation, firstPointZ));
                    if (firstPointZ == safeHeight)
                    {
                        commands.Add("waitEndMove()");
                    }
                }
                isFirstCommand = false;

                commands.Add(FormatCommand("movej", startPoint, startOrientation, drawHeight));
                commands.Add(FormatCommand("movel", endPoint, endOrientation, drawHeight));
                inDrawing = true;

                lastPoint = endPoint;
                lastZ = drawHeight;
            }

            // Process curves
            foreach (var curve in curves)
            {
                for (int i = 0; i < curve.Size; i++)
                {
                    Point point = curve[i];
                    var (robotX, robotY) = DefinePoint(point.X, point.Y, scaleFactor, marginX, marginY);

                    Point robotPoint = new Point((int)robotX, (int)robotY);
                    double[] orientation = (i == 0) ? defaultOrientation : CalculateOrientationForPoint(robotPoint);
                    string commandType = (i == 0) ? "movej" : "movel";

                    if (!isFirstCommand && lastZ == safeHeight && !inDrawing)
                    {
                        commands.Add("waitEndMove()");
                    }

                    double pointZ = (i == 0 && isFirstCommand) ? safeHeight : drawHeight;
                    commands.Add(FormatCommand(commandType, robotPoint, orientation, pointZ));
                    if (pointZ == safeHeight)
                    {
                        commands.Add("waitEndMove()");
                        inDrawing = false;
                    }
                    else if (commandType == "movel")
                    {
                        inDrawing = true;
                    }

                    isFirstCommand = false;

                    if (i == curve.Size - 1)
                    {
                        lastPoint = robotPoint;
                        lastZ = drawHeight;
                    }
                }
            }

            return commands;
        }
        #endregion

        #region Yardımcı Metotlar
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

        private string FormatCommand(string commandType, Point point, double[] orientation)
        {
            // Assumes that the orientation array contains [Rx, Ry, Rz]
            return $"{commandType}(appro(pPoint1,{{ {point.X}, {point.Y}, {GetZValue(point)}, {orientation[0]}, {orientation[1]}, {orientation[2]} }}), tTool, mFast)";
        }
        private string FormatCommand(string commandType, Point point, double[] orientation, double zValue)
        {
            return $"{commandType}(appro(pPoint1,{{ {point.X}, {point.Y}, {zValue}, {orientation[0]}, {orientation[1]}, {orientation[2]} }}), tTool, mFast)";
        }

        private bool isFirstPoint = true;
        private double GetZValue(Point point)
        {
            if (isFirstPoint)
            {
                isFirstPoint = false;
                return 0;
            }

            return 20;
        }

        private void ResetFirstPoint()
        {
            isFirstPoint = true;
        }

        private double[] CalculateOrientationForPoint(Point point)
        {
            double rx = 0;
            double ry = 0;
            double rz = 0;

            return new double[] { rx, ry, rz };
        }

        private List<Point> SortPathPoints(List<Point> points)
        {
            if (points.Count == 0) return new List<Point>();

            List<Point> sortedPoints = new List<Point>();
            Point currentPoint = points[0];
            sortedPoints.Add(currentPoint);
            points.Remove(currentPoint);

            while (points.Count > 0)
            {
                currentPoint = points.Aggregate((current, next) => Distance(currentPoint, next) < Distance(currentPoint, current) ? next : current);
                sortedPoints.Add(currentPoint);
                points.Remove(currentPoint);
            }

            return sortedPoints;
        }

        private double Distance(Point p1, Point p2)
        {
            return Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));
        }

        private (int minX, int minY, int maxX, int maxY) CalculateBoundingBox(List<Point> points)
        {
            int minX = points.Min(p => p.X);
            int minY = points.Min(p => p.Y);
            int maxX = points.Max(p => p.X);
            int maxY = points.Max(p => p.Y);
            return (minX, minY, maxX, maxY);
        }

        private List<Point> TranslatePoints(List<Point> points, int offsetX, int offsetY)
        {
            return points.Select(p => new Point(p.X + offsetX, p.Y + offsetY)).ToList();
        }

        public static (double, double) DefinePoint(double imageX, double imageY, double scaleFactor)
        {
            double robotX = imageX * scaleFactor;
            double robotY = imageY * scaleFactor;
            return (robotX, robotY);
        }

        public static (double, double) DefinePoint(double imageX, double imageY, double scaleFactor, double marginX, double marginY)
        {
            double robotX = imageX * scaleFactor + marginX;
            double robotY = imageY * scaleFactor + marginY;
            return (robotX, robotY);
        }

        private void SaveCommandsAsXml(string filePath, List<string> commands)
        {
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                sw.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sw.WriteLine("<Programs xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://www.staubli.com/robotics/VAL3/Program/2\">");
                sw.WriteLine("  <Program name=\"start\">");
                sw.WriteLine("    <Code><![CDATA[");
                sw.WriteLine("begin");
                sw.WriteLine("cls()");
                sw.WriteLine("close(tTool)");
                sw.WriteLine("open(tTool1)");
                sw.WriteLine("nError=setFrame(pOrigin,pX,pY,fMasa)");
                foreach (string command in commands)
                {
                    sw.WriteLine(command);
                }
                sw.WriteLine("waitEndMove()");
                sw.WriteLine("end");
                sw.WriteLine("]]></Code>");
                sw.WriteLine("  </Program>");
                sw.WriteLine("</Programs>");
            }
        }

        private void SaveCommandsAsText(string filePath, List<string> commands)
        {
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                sw.WriteLine("begin");
                sw.WriteLine("cls()");
                sw.WriteLine("close(tTool)");
                sw.WriteLine("open(tTool1)");
                sw.WriteLine("nError=setFrame(pOrigin,pX,pY,fMasa)");
                foreach (string command in commands)
                {
                    sw.WriteLine(command);
                }
                sw.WriteLine("waitEndMove()");
                sw.WriteLine("end");
            }
        }

        private List<Point> SmoothPath(List<Point> path, int windowSize = 5)
        {
            List<Point> smoothedPath = new List<Point>();
            for (int i = 0; i < path.Count; i++)
            {
                int sumX = 0, sumY = 0, count = 0;
                for (int j = -windowSize; j <= windowSize; j++)
                {
                    int idx = i + j;
                    if (idx >= 0 && idx < path.Count)
                    {
                        sumX += path[idx].X;
                        sumY += path[idx].Y;
                        count++;
                    }
                }
                smoothedPath.Add(new Point(sumX / count, sumY / count));
            }
            return smoothedPath;
        }

        private void AdjustPointsToCenter(List<Point> points)
        {
            int minX = points.Min(p => p.X);
            int minY = points.Min(p => p.Y);
            int maxX = points.Max(p => p.X);
            int maxY = points.Max(p => p.Y);

            int centerX = (minX + maxX) / 2;
            int centerY = (minY + maxY) / 2;

            int offsetX = (pcbDrawing.Width / 2) - centerX;
            int offsetY = (pcbDrawing.Height / 2) - centerY;

            for (int i = 0; i < points.Count; i++)
            {
                points[i] = new Point(points[i].X + offsetX, points[i].Y + offsetY);
            }
        }

        #endregion

        #region Değişkenler

        private double scaleFactor = 0.5;
        private double marginX = 50; // Margin from the left
        private double marginY = 50; // Margin from the top

        #endregion

        #region Drawing Commands

        private void DrawRobotCommands(List<string> commands)
        {
            Bitmap drawingImage = new Bitmap(pcbDrawing.Width, pcbDrawing.Height);
            using (Graphics g = Graphics.FromImage(drawingImage))
            {
                g.Clear(Color.White);
                Pen pen = new Pen(Color.Black, 2);

                Point? lastDrawingPoint = null;
                bool inDrawing = false;

                foreach (var command in commands)
                {
                    if (command.StartsWith("movej") || command.StartsWith("movel"))
                    {
                        Point point = ParsePointFromCommand(command);
                        if (command.StartsWith("movej"))
                        {
                            if (inDrawing)
                            {
                                g.DrawLine(pen, lastDrawingPoint.Value, point);
                                inDrawing = false;
                            }
                            lastDrawingPoint = point;
                        }
                        else if (command.StartsWith("movel"))
                        {
                            if (lastDrawingPoint.HasValue)
                            {
                                g.DrawLine(pen, lastDrawingPoint.Value, point);
                            }
                            lastDrawingPoint = point;
                            inDrawing = true;
                        }
                    }
                    else if (command.StartsWith("waitEndMove()"))
                    {
                        inDrawing = false;
                    }
                }
            }

            pcbDrawing.Image = drawingImage;
            pcbDrawing.SizeMode = PictureBoxSizeMode.StretchImage;
        }

        private Point ParsePointFromCommand(string command)
        {
            var start = command.IndexOf('{') + 1;
            var end = command.IndexOf('}');
            var parts = command.Substring(start, end - start).Split(',');
            int x = int.Parse(parts[0].Trim());
            int y = int.Parse(parts[1].Trim());
            return new Point(x, y);
        }
        #endregion
    }
}
