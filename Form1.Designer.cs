namespace SuperVisionObjectDesktop
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            DevicesFlowLayout = new FlowLayoutPanel();
            tabControl1 = new TabControl();
            LoadPicture = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)LoadPicture).BeginInit();
            SuspendLayout();
            // 
            // DevicesFlowLayout
            // 
            DevicesFlowLayout.Name = "DevicesFlowLayout";
            DevicesFlowLayout.Size = new Size(347, 490);
            DevicesFlowLayout.TabIndex = 2;
            // 
            // tabControl1
            // 
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1040, 543);
            tabControl1.TabIndex = 4;
            // 
            // LoadPicture
            // 
            LoadPicture.Image = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resource", "load.gif"));
            LoadPicture.Name = "LoadPicture";
            LoadPicture.Size = new Size(28, 28);
            LoadPicture.SizeMode = PictureBoxSizeMode.StretchImage;
            LoadPicture.TabIndex = 5;
            LoadPicture.TabStop = false;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1422, 593);
            Name = "Form1";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)LoadPicture).EndInit();
            ResumeLayout(false);
        }

        #endregion
        private FlowLayoutPanel DevicesFlowLayout;
        private TabControl tabControl1;
        private PictureBox LoadPicture;
    }
}
