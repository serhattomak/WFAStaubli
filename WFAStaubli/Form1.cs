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
                imageBox.Image = new Bitmap(openFileDialog.FileName);
            }
        }
        private void btnConvert_Click(object sender, EventArgs e)
        {
            if (pcbOriginal.Image != null)
            {
                Bitmap originalImage = new Bitmap(pcbOriginal.Image);
                Bitmap convertedImage = ConvertToBlackAndWhite(originalImage);
                pcbConverted.Image = convertedImage;
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
            for (int i = 0; i < originalImage.Width; i++)
            {
                for (int j = 0; j < originalImage.Height; j++)
                {
                    Color originalColor = originalImage.GetPixel(i, j);
                    int grayScale = (int)((originalColor.R * 0.3) + (originalColor.G * 0.59) + (originalColor.B * 0.11));
                    Color newColor = Color.FromArgb(grayScale, grayScale, grayScale);
                    originalImage.SetPixel(i, j, newColor);
                }
            }
            return originalImage;
        }

        #endregion
    }
}
