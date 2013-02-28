#include "../../../GodComplex.h"
#include "EffectScene.h"
#include "Scene.h"

#define CHECK_MATERIAL( pMaterial, ErrorCode )		if ( (pMaterial)->HasErrors() ) m_ErrorCode = ErrorCode;

EffectScene::EffectScene( Device& _Device, Scene& _Scene, Primitive& _ScreenQuad ) : m_Device( _Device ), m_Scene( _Scene ), m_ScreenQuad( _ScreenQuad ), m_ErrorCode( 0 )
{
	//////////////////////////////////////////////////////////////////////////
	// Create the materials
	CHECK_MATERIAL( m_pMatDepthPass = CreateMaterial( IDR_SHADER_SCENE_DEPTH_PASS, VertexFormatP3N3G3T2::DESCRIPTOR, "VS", NULL, NULL ), 1 );
	CHECK_MATERIAL( m_pMatBuildLinearZ = CreateMaterial( IDR_SHADER_SCENE_BUILD_LINEARZ, VertexFormatPt4::DESCRIPTOR, "VS", NULL, "PS" ), 1 );
	CHECK_MATERIAL( m_pMatFillGBuffer = CreateMaterial( IDR_SHADER_SCENE_FILL_GBUFFER, VertexFormatP3N3G3T2::DESCRIPTOR, "VS", NULL, "PS" ), 1 );
	CHECK_MATERIAL( m_pMatIndirectLighting = CreateMaterial( IDR_SHADER_SCENE_INDIRECT_LIGHTING, VertexFormatPt4::DESCRIPTOR, "VS", NULL, "PS" ), 1 );

	D3D_SHADER_MACRO	pMacrosDirectional[] = {
		{ "LIGHT_TYPE", "0" },
		{ NULL,	NULL }
	};
	CHECK_MATERIAL( m_pMatShading_Directional_StencilPass = CreateMaterial( IDR_SHADER_SCENE_SHADING_STENCIL, VertexFormatP3T2::DESCRIPTOR, "VS", NULL, NULL, pMacrosDirectional ), 1 );
	CHECK_MATERIAL( m_pMatShading_Directional = CreateMaterial( IDR_SHADER_SCENE_SHADING, VertexFormatPt4::DESCRIPTOR, "VS", NULL, "PS", pMacrosDirectional ), 1 );
	D3D_SHADER_MACRO	pMacrosPoint[] = {
		{ "LIGHT_TYPE", "1" },
		{ NULL,	NULL }
	};
	CHECK_MATERIAL( m_pMatShading_Point_StencilPass = CreateMaterial( IDR_SHADER_SCENE_SHADING_STENCIL, VertexFormatP3T2::DESCRIPTOR, "VS", NULL, NULL, pMacrosPoint ), 1 );
	CHECK_MATERIAL( m_pMatShading_Point = CreateMaterial( IDR_SHADER_SCENE_SHADING, VertexFormatPt4::DESCRIPTOR, "VS", NULL, "PS", pMacrosPoint ), 1 );
	D3D_SHADER_MACRO	pMacrosSpot[] = {
		{ "LIGHT_TYPE", "2" },
		{ NULL,	NULL }
	};
	CHECK_MATERIAL( m_pMatShading_Spot_StencilPass = CreateMaterial( IDR_SHADER_SCENE_SHADING_STENCIL, VertexFormatP3T2::DESCRIPTOR, "VS", NULL, NULL, pMacrosSpot ), 1 );
	CHECK_MATERIAL( m_pMatShading_Spot = CreateMaterial( IDR_SHADER_SCENE_SHADING, VertexFormatPt4::DESCRIPTOR, "VS", NULL, "PS", pMacrosSpot ), 1 );

	//////////////////////////////////////////////////////////////////////////
	// Create the render targets
	int		W = m_Device.DefaultRenderTarget().GetWidth();
	int		H = m_Device.DefaultRenderTarget().GetHeight();

	m_pDepthStencilFront = new Texture2D( m_Device, W, H, DepthStencilFormatD24S8::DESCRIPTOR );
	m_pDepthStencilBack = new Texture2D( m_Device, W, H, DepthStencilFormatD24S8::DESCRIPTOR );
	m_pRTZBuffer= new Texture2D( m_Device, W, H, 1, PixelFormatRG32F::DESCRIPTOR, 1, NULL );

	m_pRTGBuffer0_2 = new Texture2D( m_Device, W, H, 3, PixelFormatRGBA16F::DESCRIPTOR, 1, NULL );
	m_pRTGBuffer3 = new Texture2D( m_Device, W, H, 1, PixelFormatRGBA16_UINT::DESCRIPTOR, 1, NULL );

	m_pRTAccumulatorDiffuseSpecular = new Texture2D( m_Device, W, H, 2, PixelFormatRGBA16F::DESCRIPTOR, 1, NULL );

	//////////////////////////////////////////////////////////////////////////
	// Create the primitives we will need to render the lights for deferred lighting
	{
		m_pPrimCylinder = new Primitive( _Device, VertexFormatP3T2::DESCRIPTOR );
		GeometryBuilder::MapperCylindrical	Mapper( 1, -0.5f, NjFloat3::UnitY );	// V will be 0 at the top and 1 at the bottom
		GeometryBuilder::BuildCylinder( 32, 1, true, *m_pPrimCylinder, Mapper );

		m_pPrimSphere = new Primitive( _Device, VertexFormatP3T2::DESCRIPTOR );
		GeometryBuilder::MapperSpherical	Mapper2;
		GeometryBuilder::BuildSphere( 32, 16, *m_pPrimSphere, Mapper2 );
	}

	//////////////////////////////////////////////////////////////////////////
	// Create the constant buffers
	m_pCB_Render = new CB<CBRender>( m_Device, 10 );
	m_pCB_Light = new CB<CBLight>( m_Device, 11 );
}

EffectScene::~EffectScene()
{
	delete m_pCB_Light;
	delete m_pCB_Render;

	delete m_pPrimSphere;
	delete m_pPrimCylinder;

	delete m_pRTAccumulatorDiffuseSpecular;

	delete m_pRTGBuffer3;
	delete m_pRTGBuffer0_2;
	delete m_pRTZBuffer;
	delete m_pDepthStencilBack;
	delete m_pDepthStencilFront;

	delete m_pMatShading_Spot;
	delete m_pMatShading_Spot_StencilPass;
	delete m_pMatShading_Point;
	delete m_pMatShading_Point_StencilPass;
	delete m_pMatShading_Directional;
	delete m_pMatShading_Directional_StencilPass;
	delete m_pMatIndirectLighting;
 	delete m_pMatFillGBuffer;
	delete m_pMatBuildLinearZ;
	delete m_pMatDepthPass;
}

void	EffectScene::Render( float _Time, float _DeltaTime, Texture2D* _pTex )
{
	int		W = m_Device.DefaultRenderTarget().GetWidth();
	int		H = m_Device.DefaultRenderTarget().GetHeight();

	m_pCB_Render->m.dUV.Set( 1.0f / W, 1.0f / H, 0.0f );

	// Update scene once
	m_Scene.Update( _Time, _DeltaTime );

	//////////////////////////////////////////////////////////////////////////
	// 1] Render scene in depth pre-pass in front & back Z Buffers
	USING_MATERIAL_START( *m_pMatDepthPass )

	// 1.1] Render front faces in front Z buffer
	m_Device.SetStates( m_Device.m_pRS_CullBack, m_Device.m_pDS_ReadWriteLess, m_Device.m_pBS_ZPrePass );

	m_Device.ClearDepthStencil( *m_pDepthStencilFront, 1.0f, 0 );

	ID3D11RenderTargetView*	ppRenderTargets[1] = { NULL };	// No render target => boost!
	m_Device.SetRenderTargets( W, H, 0, ppRenderTargets, m_pDepthStencilFront->GetDepthStencilView() );
	m_Scene.Render( M, true );

	// 1.2] Render back faces in back Z buffer
	m_Device.SetStates( m_Device.m_pRS_CullFront, m_Device.m_pDS_ReadWriteLess, m_Device.m_pBS_ZPrePass );

	m_Device.ClearDepthStencil( *m_pDepthStencilBack, 1.0f, 0 );

	m_Device.SetRenderTargets( W, H, 0, ppRenderTargets, m_pDepthStencilBack->GetDepthStencilView() );
	m_Scene.Render( M, true );

	USING_MATERIAL_END


	//////////////////////////////////////////////////////////////////////////
	// 2] Concatenate and linearize front & back Z Buffers
	USING_MATERIAL_START( *m_pMatBuildLinearZ )

	m_Device.SetStates( m_Device.m_pRS_CullNone, m_Device.m_pDS_Disabled, m_Device.m_pBS_Disabled );

	m_Device.SetRenderTarget( *m_pRTZBuffer );

	m_pDepthStencilFront->SetPS( 10 );
	m_pDepthStencilBack->SetPS( 11 );

	m_pCB_Render->UpdateData();
	m_ScreenQuad.Render( M );

	USING_MATERIAL_END


	//////////////////////////////////////////////////////////////////////////
	// 3] Render the scene in our first G-Buffer
	USING_MATERIAL_START( *m_pMatFillGBuffer )

	m_Device.SetStates( m_Device.m_pRS_CullBack, m_Device.m_pDS_ReadLessEqual, m_Device.m_pBS_Disabled );

	ID3D11RenderTargetView*	ppRenderTargets[4] =
	{
		m_pRTGBuffer0_2->GetTargetView( 0, 0, 1 ),	// Normal (XY) + Tangent (XY)
		m_pRTGBuffer0_2->GetTargetView( 0, 1, 1 ),	// Diffuse Albedo (XYZ) + Tangent (W)
		m_pRTGBuffer0_2->GetTargetView( 0, 2, 1 ),	// Specular Albedo (XYZ) + Height (W)
		m_pRTGBuffer3->GetTargetView( 0, 0, 1 )		// 4 couples of [Weight,MatId] each packed into a U16
	};
	m_Device.SetRenderTargets( W, H, 4, ppRenderTargets, m_pDepthStencilFront->GetDepthStencilView() );

	m_Scene.Render( M );

	USING_MATERIAL_END
	

	//////////////////////////////////////////////////////////////////////////
	// 4] Apply shading using my Pom materials! ^^
	m_Device.ClearRenderTarget( *m_pRTAccumulatorDiffuseSpecular, NjFloat4::Zero );

	ID3D11RenderTargetView*	ppRenderTargets[2] = { m_pRTAccumulatorDiffuseSpecular->GetTargetView( 0, 0, 1 ), m_pRTAccumulatorDiffuseSpecular->GetTargetView( 0, 1, 1 ) };
	m_Device.SetRenderTargets( W, H, 2, ppRenderTargets, m_pDepthStencilFront->GetDepthStencilView() );

	// Set our buffers for next shaders...
	m_pRTGBuffer0_2->SetPS( 10 );
	m_pRTGBuffer3->SetPS( 11 );
	m_pRTZBuffer->SetPS( 12 );

//	m_Device.SetStates( m_Device.m_pRS_CullNone, NULL, NULL );

	m_pCB_Render->UpdateData();

	// TODO: DrawInstanced with a texture buffer of light infos!

	// 4.1] Process directionals
	if ( m_Scene.GetEnabledDirectionalLightsCount() > 0 )
	{
		const Scene::Light*	pLight = m_Scene.GetDirectionalLights();
		int					LightsCount = m_Scene.GetDirectionalLightsCount();
		for ( int LightIndex=0; LightIndex < LightsCount; LightIndex++, pLight++ )
			if ( pLight->m_bEnabled )
			{
				m_pCB_Light->m.Position = pLight->m_Position;
				m_pCB_Light->m.Direction = pLight->m_Direction;
				m_pCB_Light->m.Radiance = pLight->m_Radiance;
				m_pCB_Light->m.Data.Set( pLight->m_Data.m_RadiusHotSpot, pLight->m_Data.m_RadiusFalloff, pLight->m_Data.m_Length, 0 );
				m_pCB_Light->UpdateData();

				m_Device.ClearDepthStencil( *m_pDepthStencilFront, 0, 0, false, true );	// Clear only stencil

				USING_MATERIAL_START( *m_pMatShading_Directional_StencilPass )

				m_Device.SetStates( m_Device.m_pRS_CullNone, m_Device.m_pDS_ReadLessEqual_StencilIncBackDecFront, m_Device.m_pBS_Disabled );
				m_pPrimCylinder->Render( M );

				USING_MATERIAL_END

				USING_MATERIAL_START( *m_pMatShading_Directional )

//				m_Device.SetStates( m_Device.m_pRS_CullFront, m_Device.m_pDS_ReadLessEqual_StencilFailIfZero, m_Device.m_pBS_Additive );
//				m_Device.SetStates( m_Device.m_pRS_CullFront, m_Device.m_pDS_Disabled, m_Device.m_pBS_Additive );
				m_Device.SetStates( NULL, m_Device.m_pDS_ReadLessEqual_StencilFailIfZero, m_Device.m_pBS_Additive );
//m_Device.SetStates( NULL, m_Device.m_pDS_Disabled, m_Device.m_pBS_Additive );
				m_ScreenQuad.Render( M );

				USING_MATERIAL_END
			}
	}

	// 4.2] Process spots
	if ( m_Scene.GetEnabledSpotLightsCount() > 0 )
	{
		const Scene::Light*	pLight = m_Scene.GetSpotLights();
		int					LightsCount = m_Scene.GetSpotLightsCount();
		for ( int LightIndex=0; LightIndex < LightsCount; LightIndex++, pLight++ )
			if ( pLight->m_bEnabled )
			{
				m_pCB_Light->m.Position = pLight->m_Position;
				m_pCB_Light->m.Direction = pLight->m_Direction;
				m_pCB_Light->m.Radiance = pLight->m_Radiance;
				m_pCB_Light->m.Data.Set( 0.5f * pLight->m_Data.m_AngleHotSpot, 0.5f * pLight->m_Data.m_AngleFalloff, pLight->m_Data.m_Length, tanf( 0.5f * pLight->m_Data.m_AngleFalloff ) );
				m_pCB_Light->UpdateData();

				m_Device.ClearDepthStencil( *m_pDepthStencilFront, 0, 0, false, true );	// Clear only stencil

				USING_MATERIAL_START( *m_pMatShading_Spot_StencilPass )

				m_Device.SetStates( m_Device.m_pRS_CullNone, m_Device.m_pDS_ReadLessEqual_StencilIncBackDecFront, m_Device.m_pBS_Disabled );
				m_pPrimCylinder->Render( M );

				USING_MATERIAL_END

				USING_MATERIAL_START( *m_pMatShading_Spot )

//				m_Device.SetStates( m_Device.m_pRS_CullFront, m_Device.m_pDS_ReadLessEqual_StencilFailIfZero, m_Device.m_pBS_Additive );
//				m_Device.SetStates( m_Device.m_pRS_CullFront, m_Device.m_pDS_Disabled, m_Device.m_pBS_Additive );
				m_Device.SetStates( NULL, m_Device.m_pDS_ReadLessEqual_StencilFailIfZero, m_Device.m_pBS_Additive );
 				m_ScreenQuad.Render( M );

				USING_MATERIAL_END
			}
	}

	// 4.3] Process points
	if ( m_Scene.GetEnabledPointLightsCount() > 0 )
	{
		const Scene::Light*	pLight = m_Scene.GetPointLights();
		int					LightsCount = m_Scene.GetPointLightsCount();
		for ( int LightIndex=0; LightIndex < LightsCount; LightIndex++, pLight++ )
			if ( pLight->m_bEnabled )
			{
				m_pCB_Light->m.Position = pLight->m_Position;
				m_pCB_Light->m.Radiance = pLight->m_Radiance;
				m_pCB_Light->m.Data.Set( pLight->m_Data.m_Radius, 0, 0, 0 );
				m_pCB_Light->UpdateData();

				m_Device.ClearDepthStencil( *m_pDepthStencilFront, 0, 0, false, true );	// Clear only stencil

				USING_MATERIAL_START( *m_pMatShading_Point_StencilPass )

				m_Device.SetStates( m_Device.m_pRS_CullNone, m_Device.m_pDS_ReadLessEqual_StencilIncBackDecFront, m_Device.m_pBS_Disabled );
				m_pPrimSphere->Render( M );

				USING_MATERIAL_END

				USING_MATERIAL_START( *m_pMatShading_Point )

// 				m_Device.SetStates( m_Device.m_pRS_CullFront, m_Device.m_pDS_ReadLessEqual_StencilFailIfZero, m_Device.m_pBS_Additive );
				m_Device.SetStates( NULL, m_Device.m_pDS_ReadLessEqual_StencilFailIfZero, m_Device.m_pBS_Additive );
				m_ScreenQuad.Render( M );

				USING_MATERIAL_END
			}
	}


	//////////////////////////////////////////////////////////////////////////
	// 5] Apply indirect lighting with importance sampling
	USING_MATERIAL_START( *m_pMatIndirectLighting )

	m_Device.SetStates( m_Device.m_pRS_CullNone, m_Device.m_pDS_Disabled, m_Device.m_pBS_Disabled );
	m_Device.SetRenderTarget( m_Device.DefaultRenderTarget(), &m_Device.DefaultDepthStencil() );

	m_pRTAccumulatorDiffuseSpecular->SetPS( 13 );

_pTex->SetPS( 14 );	// DEBUG
	

	m_pCB_Render->UpdateData();
	m_ScreenQuad.Render( M );

	USING_MATERIAL_END
}
