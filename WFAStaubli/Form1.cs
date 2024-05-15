using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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

        #region Buttons
        private void btnUpload_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png",
                Title = "Select an Image"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
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
                Bitmap convertedImage = pcbConverted.Image as Bitmap;

                if (convertedImage == null || convertedImage.Width == 0 || convertedImage.Height == 0)
                {
                    MessageBox.Show("Converted image is empty or not a Bitmap.");
                    return;
                }

                List<Point> pathPoints = CreatePathFromImage(convertedImage);
                List<Point> simplifiedPoints = SimplifyPath(pathPoints, 5);
                List<Point> smoothedPoints = SmoothPath(simplifiedPoints);

                var (refinedLines, refinedCurves) = IdentifyLinesAndCurves(smoothedPoints);

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Text Files (*.txt)|*.txt|PGX Files (*.pgx)|*.pgx",
                    Title = "Save Robot Commands"
                };

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    List<string> commands = GenerateRobotCommands(refinedLines, refinedCurves);
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
                List<Point> simplifiedPoints = SimplifyPath(pathPoints, 5);
                List<Point> smoothedPoints = SmoothPath(simplifiedPoints);

                var (refinedLines, refinedCurves) = IdentifyLinesAndCurves(smoothedPoints);
                List<string> commands = GenerateRobotCommands(refinedLines, refinedCurves);

                DrawRobotCommands(commands);
            }
        }

        #endregion

        #region Black-and-White Conversion

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

        #region Path Creation

        private List<Point> CreatePathFromImage(Bitmap image)
        {
            List<Point> path = new List<Point>();
            Mat imageMat = image.ToMat();
            CvInvoke.CvtColor(imageMat, imageMat, ColorConversion.Bgr2Gray);
            CvInvoke.Threshold(imageMat, imageMat, 128, 255, ThresholdType.Binary);

            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
            {
                CvInvoke.FindContours(imageMat, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);
                for (int i = 0; i < contours.Size; i++)
                {
                    VectorOfPoint contour = contours[i];
                    for (int j = 0; j < contour.Size; j++)
                    {
                        Point point = contour[j];
                        path.Add(point);
                    }
                }
            }
            return path;
        }

        #endregion

        #region Line and Curve Identification

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
            int proximityThreshold = 10;

            foreach (Point point in path)
            {
                if (currentGroup.Count == 0)
                {
                    currentGroup.Add(point);
                }
                else
                {
                    Point lastPoint = currentGroup.Last();
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

            return groups;
        }

        private bool IsLine(List<Point> points)
        {
            if (points.Count < 2)
                return false;

            Point startPoint = points.First();
            Point endPoint = points.Last();

            double tolerance = 5.0;

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

        #region Robot Command Generation

        private List<string> GenerateRobotCommands(List<LineSegment2D> lines, List<VectorOfPoint> curves)
        {
            List<string> commands = new List<string>();
            double safeHeight = 0;
            double drawHeight = 20;
            bool isFirstCommand = true;

            commands.Add(FormatCommand("movej", DefinePoint(0, 0, scaleFactor), safeHeight));
            commands.Add("waitEndMove()");

            void AddMoveCommands(Point start, Point end, bool isFirstMove)
            {
                if (isFirstMove)
                {
                    commands.Add(FormatCommand("movej", start, safeHeight));
                    commands.Add("waitEndMove()");
                }
                commands.Add(FormatCommand("movej", start, drawHeight));
                commands.Add(FormatCommand("movel", end, drawHeight));
                commands.Add(FormatCommand("movej", end, safeHeight));
                commands.Add("waitEndMove()");
            }

            foreach (var line in lines)
            {
                Point start = DefinePoint(line.P1.X, line.P1.Y, scaleFactor);
                Point end = DefinePoint(line.P2.X, line.P2.Y, scaleFactor);

                AddMoveCommands(start, end, isFirstCommand);
                isFirstCommand = false;
            }

            foreach (var curve in curves)
            {
                for (int i = 0; i < curve.Size; i++)
                {
                    Point point = DefinePoint(curve[i].X, curve[i].Y, scaleFactor);

                    if (i == 0)
                    {
                        commands.Add(FormatCommand("movej", point, safeHeight));
                        commands.Add("waitEndMove()");
                        commands.Add(FormatCommand("movej", point, drawHeight));
                    }
                    else
                    {
                        commands.Add(FormatCommand("movel", point, drawHeight));
                    }
                }

                Point lastPoint = DefinePoint(curve[curve.Size - 1].X, curve[curve.Size - 1].Y, scaleFactor);
                commands.Add(FormatCommand("movej", lastPoint, safeHeight));
                commands.Add("waitEndMove()");

                isFirstCommand = false;
            }

            // Remove consecutive "waitEndMove()" commands
            for (int i = commands.Count - 1; i > 0; i--)
            {
                if (commands[i] == "waitEndMove()" && commands[i - 1] == "waitEndMove()")
                {
                    commands.RemoveAt(i);
                }
            }

            // Filter out redundant movej commands (i.e., consecutive movej to the same point)
            for (int i = commands.Count - 1; i > 1; i--)
            {
                if (commands[i].StartsWith("movej") && commands[i - 1].StartsWith("movej") && commands[i] == commands[i - 1])
                {
                    commands.RemoveAt(i);
                }
            }

            return commands;
        }

        #endregion

        #region Helper Methods

        private void UpdateDebugInfo(string text)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => debugLabel.Text = text));
            }
            else
            {
                debugLabel.Text = text;
            }
        }

        private string FormatCommand(string commandType, Point point, double zValue)
        {
            return $"{commandType}(appro(pPoint1,{{ {point.X}, {point.Y}, {zValue}, 0, 0, 0 }}), tTool, mFast)";
        }

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

        private Point DefinePoint(double imageX, double imageY, double scaleFactor)
        {
            int robotX = (int)(imageX * scaleFactor);
            int robotY = (int)(imageY * scaleFactor);
            return new Point(robotX, robotY);
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
                sw.WriteLine("end");
            }
        }

        private List<Point> SmoothPath(List<Point> points, int windowSize = 5)
        {
            List<Point> smoothedPoints = new List<Point>();
            int halfWindow = windowSize / 2;
            for (int i = 0; i < points.Count; i++)
            {
                double sumX = 0;
                double sumY = 0;
                int count = 0;
                for (int j = -halfWindow; j <= halfWindow; j++)
                {
                    int idx = i + j;
                    if (idx >= 0 && idx < points.Count)
                    {
                        sumX += points[idx].X;
                        sumY += points[idx].Y;
                        count++;
                    }
                }
                smoothedPoints.Add(new Point((int)(sumX / count), (int)(sumY / count)));
            }
            return smoothedPoints;
        }

        private List<Point> SimplifyPath(List<Point> points, double epsilon)
        {
            List<Point> simplified = new List<Point>();
            if (points.Count > 0)
            {
                simplified.Add(points.First());
                for (int i = 1; i < points.Count - 1; i++)
                {
                    if (Distance(points[i], simplified.Last()) > epsilon)
                    {
                        simplified.Add(points[i]);
                    }
                }
                simplified.Add(points.Last());
            }
            return simplified;
        }

        private double Distance(Point a, Point b)
        {
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) + (Math.Pow(a.Y - b.Y, 2)));
        }

        #endregion

        #region Drawing Commands

        private void DrawRobotCommands(List<string> commands)
        {
            Bitmap drawingImage = new Bitmap(pcbDrawing.Width, pcbDrawing.Height);
            using (Graphics g = Graphics.FromImage(drawingImage))
            {
                g.Clear(Color.White);
                Pen pen = new Pen(Color.Black, 2);

                Point? lastPoint = null;

                // Determine the bounding box of the points
                int minX = int.MaxValue, minY = int.MaxValue;
                int maxX = int.MinValue, maxY = int.MinValue;

                List<Point> allPoints = new List<Point>();

                foreach (var command in commands)
                {
                    if (command.StartsWith("movej") || command.StartsWith("movel"))
                    {
                        Point point = ParsePointFromCommand(command);
                        allPoints.Add(point);

                        if (point.X < minX) minX = point.X;
                        if (point.Y < minY) minY = point.Y;
                        if (point.X > maxX) maxX = point.X;
                        if (point.Y > maxY) maxY = point.Y;
                    }
                }

                if (allPoints.Count == 0) return;

                // Calculate scaling factor to fit the drawing within the PictureBox
                double scaleX = (double)pcbDrawing.Width / (maxX - minX);
                double scaleY = (double)pcbDrawing.Height / (maxY - minY);
                double scale = Math.Min(scaleX, scaleY);

                // Calculate offsets to center the drawing
                int offsetX = (int)((pcbDrawing.Width - (maxX - minX) * scale) / 2);
                int offsetY = (int)((pcbDrawing.Height - (maxY - minY) * scale) / 2);

                foreach (var point in allPoints)
                {
                    Point scaledPoint = new Point(
                        (int)((point.X - minX) * scale) + offsetX,
                        (int)((point.Y - minY) * scale) + offsetY
                    );

                    if (lastPoint.HasValue)
                    {
                        g.DrawLine(pen, lastPoint.Value, scaledPoint);
                    }

                    lastPoint = scaledPoint;
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

        #region Variables

        private double scaleFactor = 0.5;
        private bool isFirstPoint = true;

        #endregion
    }
}
