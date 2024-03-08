namespace WFAStaubli
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblOriginal = new System.Windows.Forms.Label();
            this.lblConverted = new System.Windows.Forms.Label();
            this.pcbOriginal = new System.Windows.Forms.PictureBox();
            this.pcbConverted = new System.Windows.Forms.PictureBox();
            this.btnUpload = new System.Windows.Forms.Button();
            this.btnConvert = new System.Windows.Forms.Button();
            this.btnCommand = new System.Windows.Forms.Button();
            this.debugLabel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pcbOriginal)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pcbConverted)).BeginInit();
            this.SuspendLayout();
            // 
            // lblOriginal
            // 
            this.lblOriginal.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.lblOriginal.Location = new System.Drawing.Point(100, 10);
            this.lblOriginal.Name = "lblOriginal";
            this.lblOriginal.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lblOriginal.Size = new System.Drawing.Size(300, 30);
            this.lblOriginal.TabIndex = 0;
            this.lblOriginal.Text = "YÜKLENEN GÖRÜNTÜ";
            this.lblOriginal.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // lblConverted
            // 
            this.lblConverted.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.lblConverted.Location = new System.Drawing.Point(696, 10);
            this.lblConverted.Name = "lblConverted";
            this.lblConverted.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lblConverted.Size = new System.Drawing.Size(300, 30);
            this.lblConverted.TabIndex = 1;
            this.lblConverted.Text = "DÖNÜŞTÜRÜLEN GÖRÜNTÜ";
            this.lblConverted.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // pcbOriginal
            // 
            this.pcbOriginal.Location = new System.Drawing.Point(100, 75);
            this.pcbOriginal.Name = "pcbOriginal";
            this.pcbOriginal.Size = new System.Drawing.Size(400, 400);
            this.pcbOriginal.TabIndex = 2;
            this.pcbOriginal.TabStop = false;
            // 
            // pcbConverted
            // 
            this.pcbConverted.Location = new System.Drawing.Point(700, 75);
            this.pcbConverted.Name = "pcbConverted";
            this.pcbConverted.Size = new System.Drawing.Size(400, 400);
            this.pcbConverted.TabIndex = 3;
            this.pcbConverted.TabStop = false;
            // 
            // btnUpload
            // 
            this.btnUpload.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.btnUpload.Location = new System.Drawing.Point(100, 500);
            this.btnUpload.Name = "btnUpload";
            this.btnUpload.Size = new System.Drawing.Size(400, 50);
            this.btnUpload.TabIndex = 4;
            this.btnUpload.Text = "GÖRSEL YÜKLE";
            this.btnUpload.UseVisualStyleBackColor = true;
            this.btnUpload.Click += new System.EventHandler(this.btnUpload_Click);
            // 
            // btnConvert
            // 
            this.btnConvert.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.btnConvert.Location = new System.Drawing.Point(100, 555);
            this.btnConvert.Name = "btnConvert";
            this.btnConvert.Size = new System.Drawing.Size(400, 50);
            this.btnConvert.TabIndex = 5;
            this.btnConvert.Text = "GÖRSELİ DÖNÜŞTÜR";
            this.btnConvert.UseVisualStyleBackColor = true;
            this.btnConvert.Click += new System.EventHandler(this.btnConvert_Click);
            // 
            // btnCommand
            // 
            this.btnCommand.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.btnCommand.Location = new System.Drawing.Point(700, 500);
            this.btnCommand.Name = "btnCommand";
            this.btnCommand.Size = new System.Drawing.Size(400, 105);
            this.btnCommand.TabIndex = 6;
            this.btnCommand.Text = "KOMUTA DÖNÜŞTÜR";
            this.btnCommand.UseVisualStyleBackColor = true;
            this.btnCommand.Click += new System.EventHandler(this.btnCommand_Click);
            // 
            // debugLabel
            // 
            this.debugLabel.AutoSize = true;
            this.debugLabel.Location = new System.Drawing.Point(97, 640);
            this.debugLabel.Name = "debugLabel";
            this.debugLabel.Size = new System.Drawing.Size(35, 13);
            this.debugLabel.TabIndex = 7;
            this.debugLabel.Text = "label1";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1264, 681);
            this.Controls.Add(this.debugLabel);
            this.Controls.Add(this.btnCommand);
            this.Controls.Add(this.btnConvert);
            this.Controls.Add(this.btnUpload);
            this.Controls.Add(this.pcbConverted);
            this.Controls.Add(this.pcbOriginal);
            this.Controls.Add(this.lblConverted);
            this.Controls.Add(this.lblOriginal);
            this.Name = "Form1";
            this.Text = "Image to Command Converter";
            ((System.ComponentModel.ISupportInitialize)(this.pcbOriginal)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pcbConverted)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblOriginal;
        private System.Windows.Forms.Label lblConverted;
        private System.Windows.Forms.PictureBox pcbOriginal;
        private System.Windows.Forms.PictureBox pcbConverted;
        private System.Windows.Forms.Button btnUpload;
        private System.Windows.Forms.Button btnConvert;
        private System.Windows.Forms.Button btnCommand;
        private System.Windows.Forms.Label debugLabel;
    }
}

