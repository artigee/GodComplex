#pragma once

template<typename> class CB;

class EffectGlobalIllum2 : public Scene::ISceneTagger, public Scene::ISceneRenderer
{
private:	// CONSTANTS

	static const U32		CUBE_MAP_SIZE = 128;
	static const U32		MAX_NEIGHBOR_PROBES = 32;

	static const U32		MAX_LIGHTS = 64;
	static const U32		MAX_PROBE_SETS = 16;
	static const U32		MAX_SET_SAMPLES = 64;				// Accept a maximum of 64 samples per set

	static const U32		MAX_PROBE_UPDATES_PER_FRAME = 16;	// Update a maximum of 16 probes per frame

	static const U32		SHADOW_MAP_SIZE = 1024;


protected:	// NESTED TYPES
	
#pragma pack( push, 4 )

	struct CBGeneral
	{
		bool		ShowIndirect;
 	};

	struct CBScene
	{
		U32			StaticLightsCount;
		U32			DynamicLightsCount;
		U32			ProbesCount;
 	};

	struct CBObject
	{
		NjFloat4x4	Local2World;	// Local=>World transform to rotate the object
 	};

	struct CBMaterial
	{
		U32			ID;
		NjFloat3	DiffuseColor;

		bool		HasDiffuseTexture;
		NjFloat3	SpecularColor;

		bool		HasSpecularTexture;
		NjFloat3	EmissiveColor;

		float		SpecularExponent;
	};

	struct CBProbe
	{
		NjFloat3	CurrentProbePosition;
		U32			NeighborProbeID;
		NjFloat3	NeighborProbePosition;
 	};

	struct CBSplat
	{
		NjFloat3	dUV;
	};

	struct CBShadowMap
	{
		NjFloat4x4	Light2World;
		NjFloat4x4	World2Light;
		NjFloat3	BoundsMin;
		float		__PAD0;
		NjFloat3	BoundsMax;
 	};

	struct CBUpdateProbes
	{
		NjFloat4	AmbientSH[9];				// Ambient sky (padded!)
 	};

	// Structured Buffers
	// Light buffer
	struct	LightStruct
	{
		Scene::Light::LIGHT_TYPE	Type;
		NjFloat3	Position;
		NjFloat3	Direction;
		NjFloat3	Color;
		NjFloat4	Parms;						// X=Falloff radius, Y=Cutoff radius, Z=Cos(Falloff angle), W=Cos(Cutoff angle)
	};

	// Runtime probes buffer
	struct RuntimeProbe 
	{
		NjFloat3	Position;
		float		Radius;
		NjFloat3	pSH[9];
	};

	// Probes update buffers
	struct RuntimeProbeUpdateInfos
	{
		U32			ProbeIndex;						// The index of the probe we're updating
		U32			SetsCount;						// Amount of sets for that probe
		U32			SamplingPointsStart;			// Index of the first sampling point for the probe
		U32			SamplingPointsCount;			// Amount of sampling points for the probe
		NjFloat3	SHStatic[9];					// Precomputed static SH (static geometry + static lights)
		float		SHOcclusion[9];					// Directional ambient occlusion for the probe

		struct	SetInfos
		{
			NjFloat3	SH[9];						// SH for the set
			U32			SamplingPointIndex;			// Index of the first sampling point
			U32			SamplingPointsCount;		// Amount of sampling points
		}	Sets[MAX_PROBE_SETS];
	};

	struct RuntimeSamplingPointInfos
	{
		NjFloat3	Position;						// World position of the sampling point
		NjFloat3	Normal;							// World normal of the sampling point
		float		Radius;							// Radius of the sampling point's disc approximation
	};

#pragma pack( pop )

	// The probe structure
	struct	ProbeStruct
	{
		Scene::Probe*	pSceneProbe;

		float			pSHOcclusion[9];		// The pre-computed SH that gives back how much of the environment is perceived in a given direction
		NjFloat3		pSHBounceStatic[9];		// The pre-computed SH that gives back how much the probe perceives of indirectly bounced static lighting on static geometry

		float			MeanDistance;			// Mean distance of all scene pixels
		float			MeanHarmonicDistance;	// Mean harmonic distance (1/sum(1/distance)) of all scene pixels
		float			MinDistance;			// Distance to closest scene pixel
		float			MaxDistance;			// Distance to farthest scene pixel
		NjFloat3		BBoxMin;				// Dimensions of the bounding box (axis-aligned) of the scene pixels
		NjFloat3		BBoxMax;

		U32				SetsCount;				// The amount of dynamic sets for that probe
		struct SetInfos
		{
			NjFloat3		Position;			// The position of the dynamic set
			NjFloat3		Normal;				// The normal of the dynamic set's plane
			NjFloat3		Tangent;			// The longest principal axis of the set's points cluster (scaled by the length of the axis)
			NjFloat3		BiTangent;			// The shortest principal axis of the set's points cluster (scaled by the length of the axis)
			NjFloat3		Albedo;				// The albedo of the dynamic set (not currently used, for info purpose)
			int				EmissiveMatID;		// The optional ID of an emissive material (default is -1, not emissive)
			NjFloat3		pSHBounce[9];		// The pre-computed SH that gives back how much the probe perceives of indirectly bounced dynamic lighting on static geometry, for each dynamic set

			U32				SamplesCount;		// The amount of samples for that probe
			struct	Sample
			{
				NjFloat3		Position;
				NjFloat3		Normal;
				float			Radius;
			}				pSamples[MAX_SET_SAMPLES];

		}				pSetInfos[MAX_PROBE_SETS];

		NjFloat3		pSHBouncedLight[9];		// The resulting bounced irradiance bounce * light(static+dynamic) for current frame

		// Clears the light bounce accumulator
		void			ClearLightBounce( const NjFloat3 _pSHAmbient[9] );

		// Computes the product of SHLight and SHBounce to get the SH coefficients for the bounced light
		void			AccumulateLightBounce( const NjFloat3 _pSHSet[9] );
	};



private:	// FIELDS

	int					m_ErrorCode;
	Device&				m_Device;
	Texture2D&			m_RTTarget;
	Primitive&			m_ScreenQuad;

	Material*			m_pMatRender;				// Displays the room
	Material*			m_pMatRenderEmissive;		// Displays the room's emissive objects (area lights)
	Material*			m_pMatRenderLights;			// Displays the lights as small emissive balls
	Material*			m_pMatRenderCubeMap;		// Renders the room into a cubemap
	Material*			m_pMatRenderNeighborProbe;	// Renders the neighbor probes as planes to form a 3D vorono� cell
	Material*			m_pCSComputeShadowMapBounds;// Computes the shadow map bounds
	Material*			m_pMatRenderShadowMap;		// Renders the directional shadowmap
	Material*			m_pMatPostProcess;			// Post-processes the result
	ComputeShader*		m_pCSUpdateProbe;			// Dynamically update probes

	// Primitives
	Scene				m_Scene;
	bool				m_bDeleteSceneTags;
	Primitive*			m_pPrimSphere;

	// Textures
	Texture2D*			m_pTexWalls;
	Texture2D*			m_pRTShadowMap;

	// Constant buffers
 	CB<CBGeneral>*		m_pCB_General;
 	CB<CBScene>*		m_pCB_Scene;
 	CB<CBObject>*		m_pCB_Object;
 	CB<CBMaterial>*		m_pCB_Material;
 	CB<CBProbe>*		m_pCB_Probe;
	CB<CBSplat>*		m_pCB_Splat;
 	CB<CBShadowMap>*	m_pCB_ShadowMap;
 	CB<CBUpdateProbes>*	m_pCB_UpdateProbes;

	// Runtime scene lights & probes
	SB<LightStruct>*	m_pSB_LightsStatic;
	SB<LightStruct>*	m_pSB_LightsDynamic;
	SB<RuntimeProbe>*	m_pSB_RuntimeProbes;

	// Probes Update
	int					m_ProbesCount;
	ProbeStruct*		m_pProbes;
	SB<RuntimeProbeUpdateInfos>*	m_pSB_RuntimeProbeUpdateInfos;
	SB<RuntimeSamplingPointInfos>*	m_pSB_RuntimeSamplingPointInfos;

	// Params
public:
	

public:		// PROPERTIES

	int			GetErrorCode() const	{ return m_ErrorCode; }


public:		// METHODS

	EffectGlobalIllum2( Device& _Device, Texture2D& _RTHDR, Primitive& _ScreenQuad, Camera& _Camera );
	~EffectGlobalIllum2();

	void		Render( float _Time, float _DeltaTime );


	// ISceneTagger Implementation
	virtual void*	TagMaterial( const Scene::Material& _Material ) const override;
	virtual void*	TagTexture( const Scene::Material::Texture& _Texture ) const override;
	virtual void*	TagNode( const Scene::Node& _Node ) const override;
	virtual void*	TagPrimitive( const Scene::Mesh& _Mesh, const Scene::Mesh::Primitive& _Primitive ) const override;

	// ISceneRenderer Implementation
	virtual void	RenderMesh( const Scene::Mesh& _Mesh, Material* _pMaterialOverride ) const override;

private:

	void			RenderShadowMap( const NjFloat3& _SunDirection );
	void			BuildSHCoeffs( const NjFloat3& _Direction, double _Coeffs[9] );
	void			BuildSHCosineLobe( const NjFloat3& _Direction, double _Coeffs[9] );
	void			BuildSHCone( const NjFloat3& _Direction, float _HalfAngle, double _Coeffs[9] );
	void			BuildSHSmoothCone( const NjFloat3& _Direction, float _HalfAngle, double _Coeffs[9] );
	void			ZHRotate( const NjFloat3& _Direction, const NjFloat3& _ZHCoeffs, double _Coeffs[9] );

	void			PreComputeProbes();
};