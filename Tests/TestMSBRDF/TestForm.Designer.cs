﻿namespace TestMSBRDF
{
	partial class TestForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose( bool disposing )
		{
			if ( disposing && (components != null) )
			{
				components.Dispose();
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.floatTrackbarControlRoughness = new Nuaj.Cirrus.Utility.FloatTrackbarControl();
			this.panelOutput = new Nuaj.Cirrus.Utility.PanelOutput(this.components);
			this.floatTrackbarControlAlbedo = new Nuaj.Cirrus.Utility.FloatTrackbarControl();
			this.buttonReload = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.floatTrackbarControlLightElevation = new Nuaj.Cirrus.Utility.FloatTrackbarControl();
			this.label3 = new System.Windows.Forms.Label();
			this.timer1 = new System.Windows.Forms.Timer(this.components);
			this.SuspendLayout();
			// 
			// floatTrackbarControlExtinction
			// 
			this.floatTrackbarControlRoughness.Location = new System.Drawing.Point(1390, 12);
			this.floatTrackbarControlRoughness.MaximumSize = new System.Drawing.Size(10000, 20);
			this.floatTrackbarControlRoughness.MinimumSize = new System.Drawing.Size(70, 20);
			this.floatTrackbarControlRoughness.Name = "floatTrackbarControlRoughness";
			this.floatTrackbarControlRoughness.RangeMin = 0F;
			this.floatTrackbarControlRoughness.Size = new System.Drawing.Size(200, 20);
			this.floatTrackbarControlRoughness.TabIndex = 0;
			this.floatTrackbarControlRoughness.Value = 0F;
			this.floatTrackbarControlRoughness.VisibleRangeMax = 1F;
			// 
			// panelOutput
			// 
			this.panelOutput.Location = new System.Drawing.Point(12, 12);
			this.panelOutput.Name = "panelOutput";
			this.panelOutput.Size = new System.Drawing.Size(1280, 720);
			this.panelOutput.TabIndex = 1;
			// 
			// floatTrackbarControlAlbedo
			// 
			this.floatTrackbarControlAlbedo.Location = new System.Drawing.Point(1390, 38);
			this.floatTrackbarControlAlbedo.MaximumSize = new System.Drawing.Size(10000, 20);
			this.floatTrackbarControlAlbedo.MinimumSize = new System.Drawing.Size(70, 20);
			this.floatTrackbarControlAlbedo.Name = "floatTrackbarControlAlbedo";
			this.floatTrackbarControlAlbedo.RangeMax = 1F;
			this.floatTrackbarControlAlbedo.RangeMin = 0F;
			this.floatTrackbarControlAlbedo.Size = new System.Drawing.Size(200, 20);
			this.floatTrackbarControlAlbedo.TabIndex = 0;
			this.floatTrackbarControlAlbedo.Value = 0.75F;
			this.floatTrackbarControlAlbedo.VisibleRangeMax = 1F;
			// 
			// buttonReload
			// 
			this.buttonReload.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonReload.Location = new System.Drawing.Point(1515, 709);
			this.buttonReload.Name = "buttonReload";
			this.buttonReload.Size = new System.Drawing.Size(75, 23);
			this.buttonReload.TabIndex = 2;
			this.buttonReload.Text = "Reload";
			this.buttonReload.UseVisualStyleBackColor = true;
			this.buttonReload.Click += new System.EventHandler(this.buttonReload_Click);
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(1319, 15);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(53, 13);
			this.label1.TabIndex = 3;
			this.label1.Text = "Roughness";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(1319, 42);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(40, 13);
			this.label2.TabIndex = 3;
			this.label2.Text = "Albedo";
			// 
			// floatTrackbarControlPhaseAnisotropy
			// 
			this.floatTrackbarControlLightElevation.Location = new System.Drawing.Point(1390, 64);
			this.floatTrackbarControlLightElevation.MaximumSize = new System.Drawing.Size(10000, 20);
			this.floatTrackbarControlLightElevation.MinimumSize = new System.Drawing.Size(70, 20);
			this.floatTrackbarControlLightElevation.Name = "floatTrackbarControlLightElevation";
			this.floatTrackbarControlLightElevation.RangeMax = 1F;
			this.floatTrackbarControlLightElevation.RangeMin = -1F;
			this.floatTrackbarControlLightElevation.Size = new System.Drawing.Size(200, 20);
			this.floatTrackbarControlLightElevation.TabIndex = 0;
			this.floatTrackbarControlLightElevation.Value = 0.6F;
			this.floatTrackbarControlLightElevation.VisibleRangeMax = 1F;
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(1319, 68);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(66, 13);
			this.label3.TabIndex = 3;
			this.label3.Text = "Light Theta";
			// 
			// timer1
			// 
			this.timer1.Enabled = true;
			this.timer1.Interval = 10;
			// 
			// GloubiForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1602, 741);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.buttonReload);
			this.Controls.Add(this.panelOutput);
			this.Controls.Add(this.floatTrackbarControlLightElevation);
			this.Controls.Add(this.floatTrackbarControlAlbedo);
			this.Controls.Add(this.floatTrackbarControlRoughness);
			this.Name = "MSBRDFTestForm";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "MSBRDF Test Form";
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private Nuaj.Cirrus.Utility.FloatTrackbarControl floatTrackbarControlRoughness;
		private Nuaj.Cirrus.Utility.PanelOutput panelOutput;
		private Nuaj.Cirrus.Utility.FloatTrackbarControl floatTrackbarControlAlbedo;
		private System.Windows.Forms.Button buttonReload;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label2;
		private Nuaj.Cirrus.Utility.FloatTrackbarControl floatTrackbarControlLightElevation;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Timer timer1;
	}
}

