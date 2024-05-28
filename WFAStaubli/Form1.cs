﻿using System;
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
        private double scaleFactor = 0.5;
        private double marginX = 10; // Margin from the left
        private double marginY = 10; // Margin from the top

        public Form1()
        {
            InitializeComponent();
        }

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

                var contours = DetectContours(convertedImage);

                List<string> commands = GenerateRobotCommands(contours);

                DrawRobotCommands(commands);
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

        private Bitmap ConvertToBlackAndWhite(Bitmap originalImage)
        {
            // Convert Bitmap to Image<Bgr, byte>
            Image<Bgr, byte> imgBgr;

            if (originalImage.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
            {
                // Handle transparency by converting transparent pixels to white
                imgBgr = new Image<Bgr, byte>(originalImage.Width, originalImage.Height);

                for (int y = 0; y < originalImage.Height; y++)
                {
                    for (int x = 0; x < originalImage.Width; x++)
                    {
                        Color pixelColor = originalImage.GetPixel(x, y);
                        if (pixelColor.A < 255)
                        {
                            imgBgr.Data[y, x, 0] = 255; // Blue
                            imgBgr.Data[y, x, 1] = 255; // Green
                            imgBgr.Data[y, x, 2] = 255; // Red
                        }
                        else
                        {
                            imgBgr.Data[y, x, 0] = pixelColor.B;
                            imgBgr.Data[y, x, 1] = pixelColor.G;
                            imgBgr.Data[y, x, 2] = pixelColor.R;
                        }
                    }
                }
            }
            else
            {
                imgBgr = originalImage.ToImage<Bgr, byte>();
            }

            // Convert to grayscale
            Mat matImage = new Mat();
            CvInvoke.CvtColor(imgBgr.Mat, matImage, ColorConversion.Bgr2Gray);
            CvInvoke.Threshold(matImage, matImage, 128, 255, ThresholdType.Binary);

            int size = 2;
            Mat element = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(size, size), new Point(-1, -1));
            CvInvoke.Erode(matImage, matImage, element, new Point(-1, -1), 1, BorderType.Default, default(MCvScalar));
            CvInvoke.Dilate(matImage, matImage, element, new Point(-1, -1), 1, BorderType.Default, default(MCvScalar));

            Console.WriteLine("Image converted to black and white.");
            return matImage.ToBitmap();
        }

        private List<VectorOfPoint> DetectContours(Bitmap image)
        {
            // Convert Bitmap to Image<Gray, byte>
            Image<Gray, byte> imgGray = image.ToImage<Gray, byte>();
            Mat matImage = imgGray.Mat;

            using (Mat cannyEdges = new Mat())
            {
                CvInvoke.Canny(matImage, cannyEdges, 100, 200);

                using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
                {
                    CvInvoke.FindContours(cannyEdges, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);

                    List<VectorOfPoint> contourList = new List<VectorOfPoint>();

                    Console.WriteLine($"Contours detected: {contours.Size}");

                    for (int i = 0; i < contours.Size; i++)
                    {
                        using (VectorOfPoint contour = contours[i])
                        {
                            Console.WriteLine($"Contour {i} size: {contour.Size}");
                            if (contour.Size > 0)
                            {
                                // Clone the contour
                                VectorOfPoint clonedContour = new VectorOfPoint();
                                clonedContour.Push(contour.ToArray());
                                contourList.Add(clonedContour);
                            }
                        }
                    }
                    return contourList;
                }
            }
        }

        private List<string> GenerateRobotCommands(List<VectorOfPoint> contours)
        {
            List<string> commands = new List<string>();

            double safeHeight = 0;
            double drawHeight = 20;
            bool isFirstCommand = true;
            bool inDrawing = false;
            double lastZ = safeHeight;

            double[] defaultOrientation = new double[3];

            commands.Add(FormatCommand("movej", new Point(0, 0), defaultOrientation, safeHeight));

            List<List<Point>> allPoints = new List<List<Point>>();
            Console.WriteLine($"Total contours: {contours.Count}");

            foreach (var contour in contours)
            {
                if (contour == null || contour.Size == 0)
                {
                    Console.WriteLine("Empty or null contour skipped.");
                    continue;
                }

                List<Point> points = new List<Point>();
                for (int i = 0; i < contour.Size; i++)
                {
                    try
                    {
                        Point point = contour[i];
                        points.Add(point);
                        Console.WriteLine($"Added point: {point}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error accessing point at index {i} of contour: {ex.Message}");
                        continue;
                    }
                }

                if (points.Count > 0)
                {
                    allPoints.Add(points);
                    Console.WriteLine($"Points in contour: {points.Count}");
                }
            }

            double minX = double.MaxValue;
            double minY = double.MaxValue;

            foreach (var points in allPoints)
            {
                foreach (var point in points)
                {
                    if (point.X < minX)
                        minX = point.X;
                    if (point.Y < minY)
                        minY = point.Y;
                }
            }

            Point lastPoint = new Point(0, 0);

            foreach (var points in allPoints)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    Point point = points[i];
                    var (robotX, robotY) = DefinePoint(point.X - minX, point.Y - minY, scaleFactor, marginX, marginY);

                    Console.WriteLine($"Processing point {i} of contour: ({point.X}, {point.Y}) -> ({robotX}, {robotY})");

                    Point robotPoint = new Point((int)robotX, (int)robotY);
                    double[] orientation = defaultOrientation;
                    string commandType = (i == 0) ? "movej" : "movel";

                    double distance = Math.Sqrt(Math.Pow(robotPoint.X - lastPoint.X, 2) + Math.Pow(robotPoint.Y - lastPoint.Y, 2));

                    if (distance >= 20)
                    {
                        commands.Add(FormatCommand("movej", lastPoint, orientation, 0));
                        commands.Add("waitEndMove()");
                        commands.Add(FormatCommand("movej", robotPoint, orientation, 0));
                        commands.Add("waitEndMove()");
                        commands.Add(FormatCommand("movel", robotPoint, orientation, drawHeight));
                    }
                    else
                    {
                        commands.Add(FormatCommand(commandType, robotPoint, orientation, drawHeight));
                    }

                    if (!isFirstCommand && lastZ == safeHeight && !inDrawing)
                    {
                        commands.Add("waitEndMove()");
                    }

                    if (commandType == "movel")
                    {
                        inDrawing = true;
                    }

                    isFirstCommand = false;

                    if (i == points.Count - 1)
                    {
                        commands.Add("waitEndMove()");
                    }

                    lastZ = drawHeight;
                    lastPoint = robotPoint;
                }
            }

            commands.Add(FormatCommand("movej", new Point(0, 0), defaultOrientation, safeHeight));
            commands.Add("waitEndMove()");

            Console.WriteLine($"Total commands generated: {commands.Count}");
            return commands;
        }

        private string FormatCommand(string commandType, Point point, double[] orientation, double zValue)
        {
            return $"{commandType}(appro(pPoint1,{{ {point.X}, {point.Y}, {zValue}, {orientation[0]}, {orientation[1]}, {orientation[2]} }}), tTool, mFast)";
        }

        private (double, double) DefinePoint(double imageX, double imageY, double scaleFactor, double marginX, double marginY)
        {
            double robotX = imageX * scaleFactor + marginX;
            double robotY = imageY * scaleFactor + marginY;
            return (robotX, robotY);
        }

        private void btnCommand_Click(object sender, EventArgs e)
        {
            try
            {
                if (pcbConverted.Image != null)
                {
                    Bitmap convertedImage = pcbConverted.Image as Bitmap;

                    if (convertedImage == null || convertedImage.Width == 0 || convertedImage.Height == 0)
                    {
                        MessageBox.Show("Converted image is empty or not a Bitmap.");
                        return;
                    }

                    var contours = DetectContours(convertedImage);

                    SaveFileDialog saveFileDialog = new SaveFileDialog
                    {
                        Filter = "Text Files (*.txt)|*.txt|PGX Files (*.pgx)|*.pgx",
                        Title = "Save Robot Commands"
                    };

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        List<string> commands = GenerateRobotCommands(contours);
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
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
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
    }
}
