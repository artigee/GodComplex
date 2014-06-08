﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;

namespace TestFresnel
{
	public partial class OutputPanel : Panel
	{
		[System.Diagnostics.DebuggerDisplay( "wl={Wavelength} n={n} k={k}" )]
		public class	RefractionData
		{
			public float	Wavelength;	// In µm
			public float	n;			// Refraction index
			public float	k;			// Extinction coefficient
		}

		protected Bitmap	m_Bitmap = null;

		public enum		FRESNEL_TYPE
		{
			SCHLICK,
			PRECISE,
		}

		protected FRESNEL_TYPE	m_Type = FRESNEL_TYPE.SCHLICK;
		public  FRESNEL_TYPE	FresnelType
		{
			get { return m_Type; }
			set
			{
				m_Type = value;
				UpdateBitmap();
			}
		}

		protected bool			m_FromData = false;
		public bool				FromData
		{
			get { return m_FromData; }
			set
			{
				m_FromData = value;
				UpdateBitmap();
			}
		}

		protected float			m_IOR = 1.0f;
		public float			IOR
		{
			get { return m_IOR; }
			set
			{
				m_IOR = value;
				UpdateBitmap();
			}
		}

		protected Color			m_SpecularTint = Color.White;
		public Color			SpecularTint
		{
			get { return m_SpecularTint; }
			set
			{
				m_SpecularTint = value;
				UpdateBitmap();
			}
		}

		protected RefractionData[]	m_Data = null;
		public RefractionData[]		Data
		{
			get { return m_Data; }
			set
			{
				m_Data = value;
				UpdateBitmap();
			}
		}

		public OutputPanel()
		{
			InitializeComponent();
		}

		public OutputPanel( IContainer container )
		{
			container.Add( this );

			InitializeComponent();
		}

		protected void		UpdateBitmap()
		{
			if ( m_Bitmap == null )
				return;

			using ( Graphics G = Graphics.FromImage( m_Bitmap ) )
			{
				G.FillRectangle( Brushes.White, 0, 0, Width, Height );

				G.DrawLine( Pens.Black, 10, 0, 10, Height );
				G.DrawLine( Pens.Black, 0, Height-10, Width, Height-10 );

				FresnelEval	Eval = null;
				if ( m_FromData )
				{
					switch ( m_Type ) 
					{
						case FRESNEL_TYPE.SCHLICK:	Eval = Fresnel_SchlickData; PrepareData(); break;
						case FRESNEL_TYPE.PRECISE:	Eval = Fresnel_PreciseData; PrepareData(); break;
					}
				}
				else
				{
					switch ( m_Type ) 
					{
						case FRESNEL_TYPE.SCHLICK:	Eval = Fresnel_Schlick; PrepareSchlick(); break;
						case FRESNEL_TYPE.PRECISE:	Eval = Fresnel_Precise; PreparePrecise(); break;
					}
				}

				DrawLine( G, 0, 1, 1, 1, Pens.Gray );

				float	x = 0.0f;
				float	yr, yg, yb;
				Eval( 1.0f, out yr, out yg, out yb );
				for ( int X=10; X <= Width; X++ )
				{
					float	px = x;
					float	pyr = yr;
					float	pyg = yg;
					float	pyb = yb;
					x = (float) (X-10.0f) / (Width - 10);

					float	CosTheta = (float) Math.Cos( x * 0.5 * Math.PI );	// Cos(theta)

					Eval( CosTheta, out yr, out yg, out yb );

					DrawLine( G, px, pyr, x, yr, Pens.Red );
					DrawLine( G, px, pyg, x, yg, Pens.LimeGreen );
					DrawLine( G, px, pyb, x, yb, Pens.Blue );
				}

				if ( !m_FromData )
				{
					Eval( 1.0f, out yr, out yg, out yb );
					float	F0 = Math.Max( Math.Max( yr, yg ), yb );
					G.DrawString( "F0 = " + F0, Font, Brushes.Black, 12.0f, Height - 30 - (Height-20) * F0 );
				}
				else
				{
					Eval( 1.0f, out yr, out yg, out yb );
					float	F0 = Math.Max( Math.Max( yr, yg ), yb );

					float	Offset = Height - 30 - 24 - (Height-20) * F0;
					if ( Offset < 40 )
						Offset = Height - (Height-20) * Math.Min( Math.Min( yr, yg ), yb );

					G.DrawString( "R (n = " + m_IndicesR.n + " k = " + m_IndicesR.k + ") F0 = " + yr, Font, Brushes.Black, 12.0f, Offset );
					G.DrawString( "G (n = " + m_IndicesG.n + " k = " + m_IndicesG.k + ") F0 = " + yg, Font, Brushes.Black, 12.0f, Offset + 12 );
					G.DrawString( "B (n = " + m_IndicesB.n + " k = " + m_IndicesB.k + ") F0 = " + yb, Font, Brushes.Black, 12.0f, Offset + 24 );
				}
			}

			Invalidate();
		}

		protected void		DrawLine( Graphics G, float x0, float y0, float x1, float y1 )
		{
			DrawLine( G, x0, y0, x1, y1, Pens.Black );
		}
		protected void		DrawLine( Graphics G, float x0, float y0, float x1, float y1, Pen _Pen )
		{
			float	X0 = 10 + (Width-20) * x0;
			float	Y0 = Height - 10 - (Height-20) * y0;
			float	X1 = 10 + (Width-20) * x1;
			float	Y1 = Height - 10 - (Height-20) * y1;
			G.DrawLine( _Pen, X0, Y0, X1, Y1 );
		}

		protected delegate void		FresnelEval( float x, out float yr, out float yg, out float yb );

		// F0 = ((n2 - n1) / (n2 + n1))²
		// Assuming n1=1 (air)
		// We look for n2 so:
		//	n2 = (1+a)/(1-a) with a = sqrt(F0)
		protected float		F0r;
		protected float		F0g;
		protected float		F0b;
		protected void		PrepareSchlick()
		{
// 			var	IOR = (1+Math.sqrt(this.fresnelF0)) / (1-Math.sqrt(this.fresnelF0));
// 			if ( !isFinite( IOR ) )
// 				IOR = 1e30;	// Simply use a huge number instead...
			float	F0 = (float) Math.Pow( (m_IOR - 1.0) / (m_IOR + 1.0), 2.0 );
			F0r = F0 * m_SpecularTint.R / 255.0f;
			F0g = F0 * m_SpecularTint.G / 255.0f;
			F0b = F0 * m_SpecularTint.B / 255.0f;
		}
		protected void		Fresnel_Schlick( float _CosTheta, out float yr, out float yg, out float yb )
		{
			float	One_Minus_CosTheta = 1.0f - _CosTheta;
			float	One_Minus_CosTheta_Pow5 = One_Minus_CosTheta * One_Minus_CosTheta;
					One_Minus_CosTheta_Pow5 *= One_Minus_CosTheta_Pow5 * One_Minus_CosTheta;

			yr = F0r + (1.0f - F0r) * One_Minus_CosTheta_Pow5;
			yg = F0g + (1.0f - F0g) * One_Minus_CosTheta_Pow5;
			yb = F0b + (1.0f - F0b) * One_Minus_CosTheta_Pow5;
		}

		/// <summary>
		/// Stolen from §5.1 http://www.cs.cornell.edu/~srm/publications/EGSR07-btdf.pdf
		/// 
		/// F = 1/2 * (g-c)²/(g+c)² * (1 + (c*(g+c) - 1)² / (c*(g-c) + 1)²)
		/// 
		/// where:
		///		g = sqrt( (n2/n1)² - 1 + c² )
		///		n2 = IOR
		///		n1 = 1 (air)
		///		c = cos(theta)
		///		theta = angle between normal and half vector
		/// </summary>
		protected void		PreparePrecise()
		{
			float	F0 = (float) Math.Pow( (m_IOR - 1.0) / (m_IOR + 1.0), 2.0 );
			F0r = F0 * m_SpecularTint.R / 255.0f;
			F0g = F0 * m_SpecularTint.G / 255.0f;
			F0b = F0 * m_SpecularTint.B / 255.0f;

			F0r = (float) ((1.0+Math.Sqrt( F0r )) / (1.0-Math.Sqrt( F0r )));
			F0g = (float) ((1.0+Math.Sqrt( F0g )) / (1.0-Math.Sqrt( F0g )));
			F0b = (float) ((1.0+Math.Sqrt( F0b )) / (1.0-Math.Sqrt( F0b )));
		}
		protected void		Fresnel_Precise( float _CosTheta, out float yr, out float yg, out float yb )
		{
			float	c = _CosTheta;
// 			double	g = Math.Sqrt( m_IOR*m_IOR - 1.0 + c*c );
// 			float	F = (float) (0.5 * Math.Pow( (g-c) / (g+c), 2.0 ) * (1.0 + Math.Pow( (c*(g+c) - 1) / (c*(g-c) + 1), 2.0 )) );


			double	g = Math.Sqrt( F0r*F0r - 1.0 + c*c );
			yr = (float) (0.5 * Math.Pow( (g-c) / (g+c), 2.0 ) * (1.0 + Math.Pow( (c*(g+c) - 1) / (c*(g-c) + 1), 2.0 )) );
			g = Math.Sqrt( F0g*F0g - 1.0 + c*c );
			yg = (float) (0.5 * Math.Pow( (g-c) / (g+c), 2.0 ) * (1.0 + Math.Pow( (c*(g+c) - 1) / (c*(g-c) + 1), 2.0 )) );
			g = Math.Sqrt( F0b*F0b - 1.0 + c*c );
			yb = (float) (0.5 * Math.Pow( (g-c) / (g+c), 2.0 ) * (1.0 + Math.Pow( (c*(g+c) - 1) / (c*(g-c) + 1), 2.0 )) );

// 			yr = F * F0r;
// 			yg = F * F0g;
// 			yb = F * F0b;
// 			yr = F0r + (1.0f - F0r) * F;
// 			yg = F0g + (1.0f - F0g) * F;
// 			yb = F0b + (1.0f - F0b) * F;
// 			yr = F0r * (1-F) + F;
// 			yg = F0g * (1-F) + F;
// 			yb = F0b * (1-F) + F;
		}

		//////////////////////////////////////////////////////////////////////////
		// Complex Fresnel data from http://refractiveindex.info
		//
		// I used the excellent "Fresnel Term Approximation for Metals" by Lazanyi and Szirmay-Kalos
		//	to work my way through complex refraction/extinction terms...
		//
		protected RefractionData	m_IndicesR = new RefractionData();
		protected RefractionData	m_IndicesG = new RefractionData();
		protected RefractionData	m_IndicesB = new RefractionData();
		#region RGB Chromaticities CIE functions
		private double[]	m_Chromas = new double[] {
390,0.16638,0.01830,0.81532,
391,0.16635,0.01846,0.81519,
392,0.16629,0.01858,0.81513,
393,0.16620,0.01867,0.81513,
394,0.16609,0.01872,0.81519,
395,0.16595,0.01874,0.81531,
396,0.16579,0.01872,0.81548,
397,0.16561,0.01867,0.81572,
398,0.16542,0.01857,0.81601,
399,0.16521,0.01844,0.81635,
400,0.16499,0.01827,0.81673,
401,0.16477,0.01807,0.81716,
402,0.16455,0.01784,0.81761,
403,0.16433,0.01761,0.81806,
404,0.16412,0.01738,0.81849,
405,0.16393,0.01718,0.81888,
406,0.16376,0.01702,0.81922,
407,0.16359,0.01688,0.81953,
408,0.16341,0.01676,0.81983,
409,0.16320,0.01664,0.82015,
410,0.16296,0.01653,0.82052,
411,0.16266,0.01640,0.82094,
412,0.16233,0.01627,0.82140,
413,0.16198,0.01614,0.82188,
414,0.16162,0.01603,0.82235,
415,0.16126,0.01594,0.82280,
416,0.16093,0.01587,0.82320,
417,0.16060,0.01583,0.82357,
418,0.16028,0.01582,0.82390,
419,0.15994,0.01584,0.82422,
420,0.15958,0.01589,0.82453,
421,0.15920,0.01597,0.82483,
422,0.15879,0.01608,0.82513,
423,0.15836,0.01622,0.82542,
424,0.15793,0.01637,0.82570,
425,0.15750,0.01653,0.82596,
426,0.15709,0.01671,0.82620,
427,0.15669,0.01690,0.82641,
428,0.15628,0.01712,0.82660,
429,0.15586,0.01738,0.82677,
430,0.15540,0.01767,0.82692,
431,0.15491,0.01802,0.82706,
432,0.15439,0.01841,0.82720,
433,0.15384,0.01883,0.82733,
434,0.15329,0.01926,0.82745,
435,0.15276,0.01968,0.82756,
436,0.15225,0.02008,0.82767,
437,0.15178,0.02047,0.82775,
438,0.15131,0.02086,0.82782,
439,0.15085,0.02128,0.82787,
440,0.15036,0.02173,0.82790,
441,0.14985,0.02225,0.82791,
442,0.14929,0.02282,0.82789,
443,0.14871,0.02343,0.82785,
444,0.14811,0.02409,0.82780,
445,0.14749,0.02478,0.82773,
446,0.14687,0.02550,0.82763,
447,0.14624,0.02625,0.82751,
448,0.14560,0.02706,0.82734,
449,0.14493,0.02795,0.82712,
450,0.14423,0.02895,0.82682,
451,0.14349,0.03007,0.82643,
452,0.14271,0.03133,0.82597,
453,0.14186,0.03271,0.82543,
454,0.14095,0.03422,0.82483,
455,0.13997,0.03584,0.82419,
456,0.13890,0.03759,0.82351,
457,0.13775,0.03945,0.82279,
458,0.13653,0.04145,0.82202,
459,0.13525,0.04359,0.82116,
460,0.13392,0.04588,0.82020,
461,0.13254,0.04834,0.81912,
462,0.13112,0.05098,0.81790,
463,0.12964,0.05384,0.81652,
464,0.12807,0.05695,0.81498,
465,0.12638,0.06036,0.81326,
466,0.12456,0.06410,0.81134,
467,0.12259,0.06822,0.80919,
468,0.12046,0.07275,0.80678,
469,0.11818,0.07773,0.80409,
470,0.11574,0.08320,0.80106,
471,0.11313,0.08922,0.79766,
472,0.11034,0.09582,0.79384,
473,0.10736,0.10307,0.78956,
474,0.10418,0.11103,0.78479,
475,0.10078,0.11975,0.77947,
476,0.09715,0.12930,0.77354,
477,0.09329,0.13971,0.76700,
478,0.08923,0.15097,0.75980,
479,0.08497,0.16308,0.75195,
480,0.08055,0.17601,0.74344,
481,0.07599,0.18973,0.73427,
482,0.07130,0.20429,0.72441,
483,0.06649,0.21974,0.71378,
484,0.06155,0.23615,0.70230,
485,0.05651,0.25359,0.68990,
486,0.05138,0.27212,0.67650,
487,0.04622,0.29169,0.66209,
488,0.04108,0.31224,0.64668,
489,0.03604,0.33365,0.63031,
490,0.03117,0.35580,0.61304,
491,0.02654,0.37853,0.59493,
492,0.02222,0.40175,0.57604,
493,0.01827,0.42531,0.55642,
494,0.01477,0.44909,0.53614,
495,0.01178,0.47294,0.51529,
496,0.00933,0.49673,0.49393,
497,0.00741,0.52049,0.47210,
498,0.00596,0.54424,0.44980,
499,0.00490,0.56805,0.42705,
500,0.00418,0.59194,0.40387,
501,0.00374,0.61593,0.38033,
502,0.00364,0.63984,0.35652,
503,0.00393,0.66346,0.33260,
504,0.00471,0.68656,0.30872,
505,0.00605,0.70890,0.28505,
506,0.00802,0.73018,0.26180,
507,0.01073,0.74993,0.23934,
508,0.01424,0.76771,0.21805,
509,0.01860,0.78319,0.19821,
510,0.02382,0.79618,0.18001,
511,0.02983,0.80664,0.16352,
512,0.03645,0.81498,0.14857,
513,0.04345,0.82161,0.13495,
514,0.05062,0.82692,0.12246,
515,0.05780,0.83125,0.11095,
516,0.06485,0.83484,0.10031,
517,0.07183,0.83765,0.09052,
518,0.07882,0.83959,0.08158,
519,0.08593,0.84060,0.07347,
520,0.09322,0.84063,0.06615,
521,0.10076,0.83968,0.05957,
522,0.10849,0.83786,0.05365,
523,0.11636,0.83533,0.04831,
524,0.12431,0.83221,0.04349,
525,0.13227,0.82861,0.03912,
526,0.14020,0.82463,0.03517,
527,0.14806,0.82034,0.03160,
528,0.15582,0.81580,0.02838,
529,0.16344,0.81106,0.02550,
530,0.17090,0.80617,0.02292,
531,0.17820,0.80119,0.02062,
532,0.18536,0.79609,0.01855,
533,0.19247,0.79084,0.01668,
534,0.19958,0.78542,0.01500,
535,0.20674,0.77980,0.01346,
536,0.21399,0.77394,0.01207,
537,0.22130,0.76790,0.01080,
538,0.22862,0.76172,0.00966,
539,0.23592,0.75544,0.00864,
540,0.24316,0.74912,0.00773,
541,0.25030,0.74278,0.00692,
542,0.25736,0.73644,0.00620,
543,0.26434,0.73010,0.00556,
544,0.27125,0.72376,0.00499,
545,0.27809,0.71742,0.00448,
546,0.28489,0.71108,0.00403,
547,0.29164,0.70474,0.00362,
548,0.29834,0.69841,0.00326,
549,0.30499,0.69208,0.00293,
550,0.31161,0.68576,0.00263,
551,0.31821,0.67944,0.00236,
552,0.32481,0.67307,0.00211,
553,0.33148,0.66663,0.00189,
554,0.33825,0.66006,0.00170,
555,0.34516,0.65332,0.00152,
556,0.35222,0.64642,0.00136,
557,0.35937,0.63941,0.00123,
558,0.36653,0.63237,0.00110,
559,0.37363,0.62538,0.00099,
560,0.38061,0.61851,0.00089,
561,0.38743,0.61177,0.00080,
562,0.39416,0.60512,0.00072,
563,0.40087,0.59849,0.00064,
564,0.40763,0.59179,0.00058,
565,0.41450,0.58498,0.00052,
566,0.42152,0.57801,0.00047,
567,0.42864,0.57094,0.00042,
568,0.43579,0.56383,0.00038,
569,0.44293,0.55672,0.00034,
570,0.45001,0.54968,0.00031,
571,0.45698,0.54274,0.00028,
572,0.46388,0.53587,0.00025,
573,0.47071,0.52906,0.00023,
574,0.47751,0.52229,0.00020,
575,0.48429,0.51553,0.00019,
576,0.49105,0.50878,0.00017,
577,0.49782,0.50203,0.00015,
578,0.50458,0.49529,0.00014,
579,0.51133,0.48854,0.00013,
580,0.51808,0.48181,0.00011,
581,0.52481,0.47509,0.00010,
582,0.53145,0.46846,0.00009,
583,0.53795,0.46197,0.00008,
584,0.54425,0.45567,0.00008,
585,0.55031,0.44962,0.00007,
586,0.55610,0.44384,0.00006,
587,0.56167,0.43828,0.00006,
588,0.56707,0.43288,0.00005,
589,0.57236,0.42759,0.00005,
590,0.57757,0.42238,0.00004,
591,0.58273,0.41723,0.00004,
592,0.58783,0.41214,0.00004,
593,0.59283,0.40714,0.00003,
594,0.59772,0.40225,0.00003,
595,0.60249,0.39748,0.00003,
596,0.60712,0.39285,0.00003,
597,0.61163,0.38834,0.00002,
598,0.61604,0.38394,0.00002,
599,0.62034,0.37963,0.00002,
600,0.62457,0.37541,0.00002,
601,0.62871,0.37127,0.00002,
602,0.63276,0.36723,0.00002,
603,0.63668,0.36330,0.00002,
604,0.64046,0.35952,0.00001,
605,0.64409,0.35589,0.00001,
606,0.64756,0.35243,0.00001,
607,0.65088,0.34911,0.00001,
608,0.65405,0.34594,0.00001,
609,0.65708,0.34291,0.00001,
610,0.66000,0.33999,0.00001,
611,0.66280,0.33719,0.00001,
612,0.66548,0.33451,0.00001,
613,0.66807,0.33193,0.00001,
614,0.67055,0.32945,0.00001,
615,0.67293,0.32706,0.00001,
616,0.67523,0.32477,0.00000,
617,0.67744,0.32256,0.00000,
618,0.67958,0.32042,0.00000,
619,0.68166,0.31834,0.00000,
620,0.68369,0.31631,0.00000,
621,0.68567,0.31433,0.00000,
622,0.68758,0.31242,0.00000,
623,0.68940,0.31060,0.00000,
624,0.69111,0.30889,0.00000,
625,0.69269,0.30731,0.00000,
626,0.69415,0.30585,0.00000,
627,0.69549,0.30451,0.00000,
628,0.69675,0.30325,0.00000,
629,0.69793,0.30207,0.00000,
630,0.69907,0.30093,0.00000,
631,0.70017,0.29983,0.00000,
632,0.70124,0.29876,0.00000,
633,0.70228,0.29772,0.00000,
634,0.70329,0.29671,0.00000,
635,0.70426,0.29574,0.00000,
636,0.70520,0.29480,0.00000,
637,0.70612,0.29388,0.00000,
638,0.70703,0.29297,0.00000,
639,0.70795,0.29205,0.00000,
640,0.70887,0.29113,0.00000,
641,0.70980,0.29020,0.00000,
642,0.71071,0.28929,0.00000,
643,0.71157,0.28843,0.00000,
644,0.71236,0.28764,0.00000,
645,0.71304,0.28696,0.00000,
646,0.71361,0.28639,0.00000,
647,0.71410,0.28590,0.00000,
648,0.71452,0.28548,0.00000,
649,0.71490,0.28510,0.00000,
650,0.71528,0.28472,0.00000,
651,0.71566,0.28434,0.00000,
652,0.71605,0.28395,0.00000,
653,0.71645,0.28355,0.00000,
654,0.71685,0.28315,0.00000,
655,0.71725,0.28275,0.00000,
656,0.71764,0.28236,0.00000,
657,0.71802,0.28198,0.00000,
658,0.71840,0.28160,0.00000,
659,0.71876,0.28124,0.00000,
660,0.71912,0.28088,0.00000,
661,0.71946,0.28054,0.00000,
662,0.71978,0.28022,0.00000,
663,0.72009,0.27991,0.00000,
664,0.72037,0.27963,0.00000,
665,0.72062,0.27938,0.00000,
666,0.72084,0.27916,0.00000,
667,0.72104,0.27896,0.00000,
668,0.72122,0.27878,0.00000,
669,0.72139,0.27861,0.00000,
670,0.72154,0.27846,0.00000,
671,0.72169,0.27831,0.00000,
672,0.72182,0.27818,0.00000,
673,0.72195,0.27805,0.00000,
674,0.72208,0.27792,0.00000,
675,0.72219,0.27781,0.00000,
676,0.72230,0.27770,0.00000,
677,0.72239,0.27761,0.00000,
678,0.72248,0.27752,0.00000,
679,0.72257,0.27743,0.00000,
680,0.72265,0.27735,0.00000,
681,0.72272,0.27728,0.00000,
682,0.72279,0.27721,0.00000,
683,0.72285,0.27715,0.00000,
684,0.72291,0.27709,0.00000,
685,0.72296,0.27704,0.00000,
686,0.72300,0.27700,0.00000,
687,0.72304,0.27696,0.00000,
688,0.72308,0.27692,0.00000,
689,0.72311,0.27689,0.00000,
690,0.72314,0.27686,0.00000,
691,0.72317,0.27683,0.00000,
692,0.72320,0.27680,0.00000,
693,0.72323,0.27677,0.00000,
694,0.72325,0.27675,0.00000,
695,0.72327,0.27673,0.00000,
696,0.72328,0.27672,0.00000,
697,0.72329,0.27671,0.00000,
698,0.72329,0.27671,0.00000,
699,0.72329,0.27671,0.00000,
700,0.72329,0.27671,0.00000,
701,0.72329,0.27671,0.00000,
702,0.72329,0.27671,0.00000,
703,0.72329,0.27671,0.00000,
704,0.72329,0.27671,0.00000,
705,0.72329,0.27671,0.00000,
706,0.72329,0.27671,0.00000,
707,0.72328,0.27672,0.00000,
708,0.72327,0.27673,0.00000,
709,0.72325,0.27675,0.00000,
710,0.72323,0.27677,0.00000,
711,0.72320,0.27680,0.00000,
712,0.72318,0.27682,0.00000,
713,0.72314,0.27686,0.00000,
714,0.72311,0.27689,0.00000,
715,0.72308,0.27692,0.00000,
716,0.72305,0.27695,0.00000,
717,0.72301,0.27699,0.00000,
718,0.72298,0.27702,0.00000,
719,0.72295,0.27705,0.00000,
720,0.72292,0.27708,0.00000,
721,0.72288,0.27712,0.00000,
722,0.72284,0.27716,0.00000,
723,0.72280,0.27720,0.00000,
724,0.72276,0.27724,0.00000,
725,0.72272,0.27728,0.00000,
726,0.72268,0.27732,0.00000,
727,0.72264,0.27736,0.00000,
728,0.72259,0.27741,0.00000,
729,0.72255,0.27745,0.00000,
730,0.72251,0.27749,0.00000,
731,0.72246,0.27754,0.00000,
732,0.72242,0.27758,0.00000,
733,0.72237,0.27763,0.00000,
734,0.72233,0.27767,0.00000,
735,0.72228,0.27772,0.00000,
736,0.72222,0.27778,0.00000,
737,0.72217,0.27783,0.00000,
738,0.72211,0.27789,0.00000,
739,0.72204,0.27796,0.00000,
740,0.72198,0.27802,0.00000,
741,0.72191,0.27809,0.00000,
742,0.72184,0.27816,0.00000,
743,0.72177,0.27823,0.00000,
744,0.72169,0.27831,0.00000,
745,0.72162,0.27838,0.00000,
746,0.72155,0.27845,0.00000,
747,0.72148,0.27852,0.00000,
748,0.72141,0.27859,0.00000,
749,0.72134,0.27866,0.00000,
750,0.72127,0.27873,0.00000,
751,0.72120,0.27880,0.00000,
752,0.72112,0.27888,0.00000,
753,0.72105,0.27895,0.00000,
754,0.72098,0.27902,0.00000,
755,0.72091,0.27909,0.00000,
756,0.72083,0.27917,0.00000,
757,0.72075,0.27925,0.00000,
758,0.72068,0.27932,0.00000,
759,0.72060,0.27940,0.00000,
760,0.72052,0.27948,0.00000,
761,0.72044,0.27956,0.00000,
762,0.72037,0.27963,0.00000,
763,0.72030,0.27970,0.00000,
764,0.72022,0.27978,0.00000,
765,0.72015,0.27985,0.00000,
766,0.72008,0.27992,0.00000,
767,0.72001,0.27999,0.00000,
768,0.71994,0.28006,0.00000,
769,0.71987,0.28013,0.00000,
770,0.71980,0.28020,0.00000,
771,0.71973,0.28027,0.00000,
772,0.71966,0.28034,0.00000,
773,0.71958,0.28042,0.00000,
774,0.71951,0.28049,0.00000,
775,0.71943,0.28057,0.00000,
776,0.71936,0.28064,0.00000,
777,0.71928,0.28072,0.00000,
778,0.71920,0.28080,0.00000,
779,0.71912,0.28088,0.00000,
780,0.71904,0.28096,0.00000,
781,0.71895,0.28105,0.00000,
782,0.71887,0.28113,0.00000,
783,0.71879,0.28121,0.00000,
784,0.71870,0.28130,0.00000,
785,0.71862,0.28138,0.00000,
786,0.71853,0.28147,0.00000,
787,0.71845,0.28155,0.00000,
788,0.71836,0.28164,0.00000,
789,0.71828,0.28172,0.00000,
790,0.71819,0.28181,0.00000,
791,0.71810,0.28190,0.00000,
792,0.71802,0.28198,0.00000,
793,0.71793,0.28207,0.00000,
794,0.71783,0.28217,0.00000,
795,0.71774,0.28226,0.00000,
796,0.71764,0.28236,0.00000,
797,0.71753,0.28247,0.00000,
798,0.71743,0.28257,0.00000,
799,0.71732,0.28268,0.00000,
800,0.71721,0.28279,0.00000,
801,0.71710,0.28290,0.00000,
802,0.71699,0.28301,0.00000,
803,0.71688,0.28312,0.00000,
804,0.71677,0.28323,0.00000,
805,0.71666,0.28334,0.00000,
806,0.71655,0.28345,0.00000,
807,0.71644,0.28356,0.00000,
808,0.71633,0.28367,0.00000,
809,0.71622,0.28378,0.00000,
810,0.71611,0.28389,0.00000,
811,0.71599,0.28401,0.00000,
812,0.71588,0.28412,0.00000,
813,0.71577,0.28423,0.00000,
814,0.71566,0.28434,0.00000,
815,0.71555,0.28445,0.00000,
816,0.71544,0.28456,0.00000,
817,0.71533,0.28467,0.00000,
818,0.71522,0.28478,0.00000,
819,0.71512,0.28488,0.00000,
820,0.71502,0.28498,0.00000,
821,0.71492,0.28508,0.00000,
822,0.71482,0.28518,0.00000,
823,0.71473,0.28527,0.00000,
824,0.71464,0.28536,0.00000,
825,0.71455,0.28545,0.00000,
826,0.71447,0.28553,0.00000,
827,0.71439,0.28561,0.00000,
828,0.71431,0.28569,0.00000,
829,0.71424,0.28576,0.00000,
830,0.71417,0.28583,0.00000,
		};
		#endregion
		protected void	PrepareData()
		{
			if ( m_Data == null )
				return;

			// Convolve precise array of data with normalized luminous response
			RefractionData	D = new RefractionData();
			double	nR = 0.0;
			double	nG = 0.0;
			double	nB = 0.0;
			double	kR = 0.0;
			double	kG = 0.0;
			double	kB = 0.0;
			int	Count = m_Chromas.Length / 4;
			for ( int i=0; i < Count; i++ )
			{
				double	wl = 1e-3 * m_Chromas[4*i+0];	// In µm
				double	r = m_Chromas[4*i+1];
				double	g = m_Chromas[4*i+2];
				double	b = m_Chromas[4*i+3];

				SampleData( (float) wl, D );

				nR += r * D.n;
				nG += g * D.n;
				nB += b * D.n;

				kR += r * D.k;
				kG += g * D.k;
				kB += b * D.k;
			}

			double	dl = 1e-3 * (m_Chromas[4] - m_Chromas[0]);	// Integration step
			m_IndicesR.n = (float) (dl * nR);
			m_IndicesR.k = (float) (dl * kR);
			m_IndicesG.n = (float) (dl * nG);
			m_IndicesG.k = (float) (dl * kG);
			m_IndicesB.n = (float) (dl * nB);
			m_IndicesB.k = (float) (dl * kB);
		}
		protected void	SampleData( float _Wavelength, RefractionData D )
		{
			for ( int i=0; i < m_Data.Length-1; i++ )
//				if ( m_Data[i].Wavelength <= _Wavelength && m_Data[i+1].Wavelength >= _Wavelength )
				if ( m_Data[i+1].Wavelength >= _Wavelength )
				{	// Found the proper interval!
					float	t = (_Wavelength - m_Data[i].Wavelength) / (m_Data[i+1].Wavelength - m_Data[i].Wavelength);
					D.Wavelength = _Wavelength;
					D.n = (1-t) * m_Data[i].n + t * m_Data[i+1].n;
					D.k = (1-t) * m_Data[i].k + t * m_Data[i+1].k;
					return;
				}
			throw new Exception( "Wavelength out of range!" );
		}
		protected float	Fresnel_SchlickMetal( float n, float k, float _CosTheta )
		{
			double	num = (n-1)*(n-1) + 4*n*Math.Pow( 1.0-_CosTheta, 5.0 ) + k*k;
			double	den = (n+1)*(n+1) + k*k;
			float	r = (float) (num / den);
			return r;
		}
		protected void	Fresnel_SchlickData( float _CosTheta, out float yr, out float yg, out float yb )
		{
			yr = Fresnel_SchlickMetal( m_IndicesR.n, m_IndicesR.k, _CosTheta );
			yg = Fresnel_SchlickMetal( m_IndicesG.n, m_IndicesG.k, _CosTheta );
			yb = Fresnel_SchlickMetal( m_IndicesB.n, m_IndicesB.k, _CosTheta );
		}

		protected float	Fresnel_PreciseMetal( float n, float k, float _CosTheta )
		{
			double	SinTheta = Math.Sqrt( 1.0 - _CosTheta*_CosTheta );
			double	SinThetaTanTheta = SinTheta * SinTheta / _CosTheta;
			double	n2k2sinTheta2 = n*n - k*k - SinTheta*SinTheta;
			double	SqrtTruc = Math.Sqrt( n2k2sinTheta2*n2k2sinTheta2 + 4.0 * n*n * k*k );
			double	Twoasquare = SqrtTruc + n2k2sinTheta2;
			double	a = Math.Sqrt( 0.5 * Twoasquare );
			double	Twobsquare = SqrtTruc - n2k2sinTheta2;
			double	b = Math.Sqrt( 0.5 * Twobsquare );

			double	Fs = (a*a + b*b - 2*a*_CosTheta + _CosTheta*_CosTheta)
					   / (a*a + b*b + 2*a*_CosTheta + _CosTheta*_CosTheta);

			double	Fp = (a*a + b*b - 2*a*SinThetaTanTheta + SinThetaTanTheta*SinThetaTanTheta)
					   / (a*a + b*b + 2*a*SinThetaTanTheta + SinThetaTanTheta*SinThetaTanTheta);
					Fp *= Fs;

			float	r = (float) (0.5 * (Fp + Fs));
			return r;
		}
		protected void	Fresnel_PreciseData( float _CosTheta, out float yr, out float yg, out float yb )
		{
			yr = Fresnel_PreciseMetal( m_IndicesR.n, m_IndicesR.k, _CosTheta );
			yg = Fresnel_PreciseMetal( m_IndicesG.n, m_IndicesG.k, _CosTheta );
			yb = Fresnel_PreciseMetal( m_IndicesB.n, m_IndicesB.k, _CosTheta );
		}

		protected override void OnSizeChanged( EventArgs e )
		{
			if ( m_Bitmap != null )
				m_Bitmap.Dispose();

			m_Bitmap = new Bitmap( Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb );
			UpdateBitmap();

			base.OnSizeChanged( e );
		}

		protected override void OnPaintBackground( PaintEventArgs e )
		{
//			base.OnPaintBackground( e );
		}

		protected override void OnPaint( PaintEventArgs e )
		{
			base.OnPaint( e );

			if ( m_Bitmap != null )
				e.Graphics.DrawImage( m_Bitmap, 0, 0 );
		}
	}
}
