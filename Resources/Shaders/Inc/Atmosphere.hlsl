////////////////////////////////////////////////////////////////////////////////////////
// Atmosphere Helpers
// From http://www-ljk.imag.fr/Publications/Basilic/com.lmc.publi.PUBLI_Article@11e7cdda2f7_f64b69/article.pdf
// Much code stolen from Bruneton's sample
//
////////////////////////////////////////////////////////////////////////////////////////
#ifndef _ATMOSPHERE_INC_
#define _ATMOSPHERE_INC_

static const float	ATMOSPHERE_THICKNESS_KM = 60.0;
static const float	GROUND_RADIUS_KM = 6360.0;
static const float	ATMOSPHERE_RADIUS_KM = GROUND_RADIUS_KM + ATMOSPHERE_THICKNESS_KM;

static const float3	EARTH_CENTER_KM = float3( 0, -GROUND_RADIUS_KM, 0 );

static const float	AVERAGE_GROUND_REFLECTANCE = 0.1;

// Rayleigh Scattering
static const float	HREF_RAYLEIGH = 8.0;
static const float3	SIGMA_SCATTERING_RAYLEIGH = float3( 0.0058, 0.0135, 0.0331 );	// For lambdas (680,550,440) nm

// Mie Scattering + Extinction
static const float	HREF_MIE = 1.2;
static const float	SIGMA_SCATTERING_MIE = 0.004;
static const float	SIGMA_EXTINCTION_MIE = SIGMA_SCATTERING_MIE / 0.9;
static const float	MIE_ANISOTROPY = 0.76;


// 4D table resolution
static const float	RESOLUTION_ALTITUDE = 32;											// W Size (Altitude)
static const float	RESOLUTION_COS_THETA = 128;											// V size (View/Zenith angle)
static const float	RESOLUTION_COS_THETA_SUN = 32;										// Horizontal size #1 (Sun/Zenith angle)
static const float	RESOLUTION_COS_GAMMA = 8;											// Horizontal size #2 (Sun/View angle)
static const float	RESOLUTION_U = RESOLUTION_COS_THETA_SUN * RESOLUTION_COS_GAMMA;		// U Size
static const float	MODULO_U = RESOLUTION_COS_GAMMA / RESOLUTION_U;						// Modulo to access each slice of Sun/View angle

static const float	NORMALIZED_SIZE_U1 = 1.0 - 1.0 / RESOLUTION_COS_THETA_SUN;
static const float	NORMALIZED_SIZE_U2 = 1.0 - 1.0 / RESOLUTION_COS_GAMMA;
static const float	NORMALIZED_SIZE_V = 1.0 - 1.0 / RESOLUTION_COS_THETA;
static const float	NORMALIZED_SIZE_W = 1.0 - 1.0 / RESOLUTION_ALTITUDE;


Texture2D	_TexTransmittance : register(t10);
Texture3D	_TexScattering : register(t11);
Texture3D	_TexIrradiance : register(t12);

Texture2D	_TexIrradianceDelta : register(t13);			// deltaE
Texture3D	_TexScatteringDelta_Rayleigh : register(t14);	// deltaSR
Texture3D	_TexScatteringDelta_Mie : register(t15);		// deltaSM
Texture3D	_TexScatteringDelta : register(t16);			// deltaJ


////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////
// Planetary Helpers
//
void	ComputeSphericalData( float3 _PositionKm, out float _AltitudeKm, out float3 _Normal )
{
	float3	Center2Position = _PositionKm - EARTH_CENTER_KM;
	float	Radius2PositionKm = length( Center2Position );
	_AltitudeKm = Radius2PositionKm - GROUND_RADIUS_KM;
	_Normal = Center2Position / Radius2PositionKm;
}

// ====== Intersections ======

// Computes the enter intersection of a ray and a sphere
// (No check for validity!)
float	SphereIntersectionEnter( float3 _PositionKm, float3 _View, float _SphereAltitudeKm )
{
	float	R = _SphereAltitudeKm + GROUND_RADIUS_KM;
	float3	D = _PositionKm - EARTH_CENTER_KM;
	float	c = dot(D,D) - R*R;
	float	b = dot(D,_View);

	float	Delta = b*b - c;

	return -b - sqrt(Delta);
}

// Computes the exit intersection of a ray and a sphere
// (No check for validity!)
float	SphereIntersectionExit( float3 _PositionKm, float3 _View, float _SphereAltitudeKm )
{
	float	R = _SphereAltitudeKm + GROUND_RADIUS_KM;
	float3	D = _PositionKm - EARTH_CENTER_KM;
	float	c = dot(D,D) - R*R;
	float	b = dot(D,_View);

	float	Delta = b*b - c;

	return -b + sqrt(Delta);
}

// Computes both intersections of a ray and a sphere
// Returns INFINITY if no hit is found
float2	SphereIntersections( float3 _PositionKm, float3 _View, float _SphereAltitudeKm )
{
	float	R = _SphereAltitudeKm + GROUND_RADIUS_KM;
	float3	D = _PositionKm - EARTH_CENTER_KM;
	float	c = dot(D,D) - R*R;
	float	b = dot(D,_View);

	float	Delta = b*b - c;
	if ( Delta < 0.0 )
		return INFINITY;

	Delta = sqrt(Delta);

	return float2( -b - Delta, -b + Delta );
}

// Computes the nearest hit between provided sphere and ground sphere
float	ComputeNearestHit( float3 _PositionKm, float3 _View, float _SphereAltitudeKm, out bool _IsGround )
{
	float2	GroundHit = SphereIntersections( _PositionKm, _View, 0.0 );
	float	SphereHit = SphereIntersectionExit( _PositionKm, _View, _SphereAltitudeKm );

	_IsGround = false;
	if ( GroundHit.x < 0.0 || SphereHit < GroundHit.x )
		return SphereHit;	// We hit the top of the atmosphere...
	
	// We hit the ground first
	_IsGround = true;
	return GroundHit.x;
}

////////////////////////////////////////////////////////////////////////////////////////
// Phase functions
float	PhaseFunctionRayleigh( float _CosPhaseAngle )
{
    return (3.0 / (16.0 * PI)) * (1.0 + _CosPhaseAngle * _CosPhaseAngle);
}

float	PhaseFunctionMie( float _CosPhaseAngle )
{
	const float	g = MIE_ANISOTROPY;
	return 1.5 * 1.0 / (4.0 * PI) * (1.0 - g*g) * pow( max( 0.0, 1.0 + (g*g) - 2.0*g*_CosPhaseAngle ), -1.5 ) * (1.0 + _CosPhaseAngle * _CosPhaseAngle) / (2.0 + g*g);
}

// Gets the full Mie RGB components from full Rayleigh RGB and only Mie Red
// This is possible because both values are proportionally related (cf. Bruneton paper, chapter 4 on Angular Precision)
// _RayleighMieRed : XYZ = C*, W=Cmie.red
float3	GetMieFromRayleighAndMieRed( float4 _RayleighMieRed )
{
	return _RayleighMieRed.xyz * (_RayleighMieRed.w * (SIGMA_SCATTERING_RAYLEIGH.x / SIGMA_SCATTERING_RAYLEIGH) / max( _RayleighMieRed.x, 1e-4 ));
}

////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////
// Tables access
float3	GetTransmittance( float _AltitudeKm, float _CosTheta )
{
	float	NormalizedAltitude = sqrt( _AltitudeKm * (1.0 / ATMOSPHERE_THICKNESS_KM) );
	float	NormalizedCosTheta = atan( (_CosTheta + 0.15) / (1.0 + 0.15) * tan(1.5) ) / 1.5;
	float2	UV = float2( NormalizedCosTheta, NormalizedAltitude );

	return _TexTransmittance.SampleLevel( LinearClamp, UV, 0.0 ).xyz;
}

float3	GetIrradiance( Texture2D _TexIrradiance, float _AltitudeKm, float _CosThetaSun )
{
    float	NormalizedAltitude = _AltitudeKm / ATMOSPHERE_THICKNESS_KM;
    float	NormalizedCosThetaSun = (_CosThetaSun + 0.2) / (1.0 + 0.2);
    float2	UV = float2( NormalizedCosThetaSun, NormalizedAltitude );

	return _TexIrradiance.SampleLevel( LinearClamp, UV, 0.0 ).xyz;
}

// Transmittance(=transparency) of atmosphere up to a given distance
// We assume the segment is not intersecting ground
float3	GetTransmittance( float _AltitudeKm, float _CosTheta, float _DistanceKm )
{
	// P0 = [0, _RadiusKm]
	// V  = [SinTheta, CosTheta]
	//
	float	RadiusKm = GROUND_RADIUS_KM + _AltitudeKm;
	float	RadiusKm2 = sqrt( RadiusKm*RadiusKm + _DistanceKm*_DistanceKm + 2.0 * RadiusKm * _CosTheta * _DistanceKm );	// sqrt[ (P0 + d.V)� ]
	float	CosTheta2 = (RadiusKm * _CosTheta + _DistanceKm) / RadiusKm2;												// dot( P0 + d.V, V ) / RadiusKm2
	float	AltitudeKm2 = RadiusKm2 - GROUND_RADIUS_KM;

	return _CosTheta > 0.0	? min( GetTransmittance( _AltitudeKm, _CosTheta ) / GetTransmittance( AltitudeKm2, CosTheta2 ), 1.0 )
							: min( GetTransmittance( AltitudeKm2, -CosTheta2 ) / GetTransmittance( _AltitudeKm, -_CosTheta ), 1.0 );
}

// Gets the zenith/view angle (cos theta), zenith/Sun angle (cos theta Sun) and view/Sun angle (cos gamma) from a 2D parameter
//#define	INSCATTER_NON_LINEAR
void GetAnglesFrom4D( float2 _UV, float3 _dUV, float _AltitudeKm, float4 dhdH, out float _CosTheta, out float _CosThetaSun, out float _CosGamma )
{
	_UV -= 0.5 * _dUV.xy;	// Remove the half pixel offset

#ifdef INSCATTER_NON_LINEAR
	float r = GROUND_RADIUS_KM + _AltitudeKm;

	if ( _UV.y < 0.5 )
	{
		float	d = 1.0 - 2.0 * _UV.y;
				d = min( max( dhdH.z, d * dhdH.w ), dhdH.w * 0.999 );

		_CosTheta = (GROUND_RADIUS_KM * GROUND_RADIUS_KM - r * r - d * d) / (2.0 * r * d);
		_CosTheta = min( _CosTheta, -sqrt( 1.0 - (GROUND_RADIUS_KM / r) * (GROUND_RADIUS_KM / r) ) - 0.001 );
	}
	else
	{
		float	d = 2.0 * (_UV.y - 0.5);
				d = min( max( dhdH.x, d * dhdH.y ), dhdH.y * 0.999 );

		_CosTheta = (ATMOSPHERE_RADIUS_KM * ATMOSPHERE_RADIUS_KM - r * r - d * d) / (2.0 * r * d);
	}
	_CosThetaSun = mod( _UV.x, MODULO_U ) / MODULO_U;

	// paper formula
	//_CosThetaSun = -(0.6 + log(1.0 - _CosThetaSun * (1.0 -  exp(-3.6)))) / 3.0;

	// better formula
	_CosThetaSun = tan( (2.0 * _CosThetaSun - 1.0 + 0.26) * 1.1 ) / tan( 1.26 * 1.1 );
	_CosGamma = lerp( -1.0, 1.0, floor( _UV.x / MODULO_U ) / (RESOLUTION_COS_THETA_SUN-1) );

#else

	_CosTheta = lerp( -1.0, 1.0, _UV.y );
	_CosThetaSun = lerp( -0.2, 1.0, fmod( _UV.x, MODULO_U ) / MODULO_U );
	_CosGamma = lerp( -1.0, 1.0, floor( _UV.x / MODULO_U ) / (RESOLUTION_COS_THETA_SUN-1) );

#endif
}

// Samples the scattering table from 4 parameters
float4	Sample4DScatteringTable( Texture3D _TexScattering, float _AltitudeKm, float _CosThetaView, float _CosThetaSun, float _CosGamma )
{
	float	r = GROUND_RADIUS_KM + _AltitudeKm;
	float	H = sqrt( ATMOSPHERE_RADIUS_KM * ATMOSPHERE_RADIUS_KM - GROUND_RADIUS_KM * GROUND_RADIUS_KM );
	float	rho = sqrt( r * r - GROUND_RADIUS_KM * GROUND_RADIUS_KM );

	float	uAltitude = 0.5 / RESOLUTION_ALTITUDE + (rho / H) * NORMALIZED_SIZE_W;

#ifdef INSCATTER_NON_LINEAR
	float	rmu = r * _CosThetaView;
	float	delta = rmu * rmu - r * r + GROUND_RADIUS_KM * GROUND_RADIUS_KM;
	float4	cst = rmu < 0.0 && delta > 0.0 ? float4( 1.0, 0.0, 0.0, 0.5 * NORMALIZED_SIZE_V ) : float4( -1.0, H * H, H, 1.0 - 0.5 * NORMALIZED_SIZE_V );
	float	uCosTheta = cst.w + (rmu * cst.x + sqrt(delta + cst.y)) / (rho + cst.z) * (0.5 - 1.0 / RESOLUTION_COS_THETA);

	// paper formula
	//float	uCosThetaSun = 0.5 / RESOLUTION_COS_THETA_SUN + max((1.0 - exp(-3.0 * _CosThetaSun - 0.6)) / (1.0 - exp(-3.6)), 0.0) * NORMALIZED_SIZE_U1;

	// better formula
	float	uCosThetaSun = 0.5 / RESOLUTION_COS_THETA_SUN + (atan( max( _CosThetaSun, -0.1975 ) * tan( 1.26 * 1.1 ) ) / 1.1 + (1.0 - 0.26)) * 0.5 * NORMALIZED_SIZE_U1;
#else
	float	uCosThetaView = 0.5 / RESOLUTION_COS_THETA + 0.5 * (_CosThetaView + 1.0) * NORMALIZED_SIZE_V;
	float	uCosThetaSun = 0.5 / RESOLUTION_COS_THETA_SUN + max( 0.2 + _CosThetaSun, 0.0 ) / 1.2 * NORMALIZED_SIZE_U1;
#endif

	float	t = 0.5 * (_CosGamma + 1.0) * (RESOLUTION_COS_GAMMA - 1.0);
	float	uGamma = floor( t );
	t = t - uGamma;

	float4	V0 = _TexScattering.SampleLevel( LinearClamp, float3( (uGamma + uCosThetaSun) / RESOLUTION_COS_GAMMA, uCosThetaView, uAltitude ), 0.0 );
	float4	V1 = _TexScattering.SampleLevel( LinearClamp, float3( (uGamma + uCosThetaSun + 1.0) / RESOLUTION_COS_GAMMA, uCosThetaView, uAltitude ), 0.0 );
	return lerp( V0, V1, t );
}

#endif	// _ATMOSPHERE_INC_
