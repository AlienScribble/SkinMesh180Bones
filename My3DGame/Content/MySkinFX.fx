#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// M A X   B O N E S 
#define MAX_BONES   180  // regular one is 72


// T E X T U R E S
sampler texSampler : register(s0)
{
	Texture  = <DiffuseTexture>;
    Filter   = Anisotropic;
    AddressU = Mirror;
    AddressV = Mirror;
};

// TRANSFORMS & CAM
float4x4 World;
float4x4 WorldViewProj;
float3x3 WorldInverseTranspose;
float3   CamPos;

// MATERIAL
float4 DiffuseColor;
float3 EmissiveColor;
float3 SpecularColor;
float  SpecularPower;
float  shine_amplify = 1;
float3 DirLight0Direction;
float3 DirLight0DiffuseColor;
float3 DirLight0SpecularColor;
float3 DirLight1Direction;
float3 DirLight1DiffuseColor;
float3 DirLight1SpecularColor;
float3 DirLight2Direction;
float3 DirLight2DiffuseColor;
float3 DirLight2SpecularColor;
float3 FogColor;
float4 FogVector;    
float4x3 Bones[MAX_BONES];


// VS INPUT TYPES
struct VSInput_TxWeights {
    float4 Position : POSITION0;
    float3 Normal   : NORMAL0;
    float2 TexCoord : TEXCOORD0;
    uint4  Indices  : BLENDINDICES0;
    float4 Weights  : BLENDWEIGHT0;
};

// VS-OUT TO PS-INPUT TYPES
struct VSOutputTx {
    float4 PositionPS : SV_Position;
    float4 Diffuse    : COLOR0;
    float4 Specular   : COLOR1;
    float2 TexCoord   : TEXCOORD0;
};
struct VSOutputPerPixTx {
    float4 PositionPS : SV_Position;
    float2 TexCoord   : TEXCOORD0;
    float4 PositionWS : TEXCOORD1;  // position in world space
    float3 NormalWS   : TEXCOORD2;  // normal in world space
    float4 Diffuse    : COLOR0;
};


// C A L C U L A T I O N   F U N C T I O N S   S T U F F ____________________
#define COMMON_VS_OUT_PARAMS vout.PositionPS = cout.Pos_PS;   vout.Diffuse = cout.Diffuse;   vout.Specular = float4(cout.Specular, cout.FogFactor);
#define COMMON_VS_OUT_PARAMS_PER_PIXEL vout.PositionPS = cout.Pos_PS;  vout.PositionWS = float4(cout.Pos_WS, cout.FogFactor);   vout.NormalWS = cout.Normal_WS;

struct CommonVSOut {
    float4 Pos_PS,  Diffuse;
    float3 Specular;
    float  FogFactor;
};
struct ColorPair {
    float3 Diffuse;
    float3 Specular;
};
struct CommonVSOutPerPixel {
    float4 Pos_PS;     // position pixel-shader
    float3 Pos_WS;     // position in world-space
    float3 Normal_WS;  // normal in world space
    float  FogFactor;
};

// S K I N  -  calculates the position and normal from weighted bone matrices
void Skin(inout VSInput_TxWeights vin) {
    float4x3 skinning = 0;    
    [unroll]
    for (int i = 0; i < 4; i++) {  skinning += Bones[vin.Indices[i]] * vin.Weights[i];  }  // looks up bone, uses bone matrix by some percentage (4 bones should add to 100%)
    vin.Position.xyz = mul(vin.Position, skinning);
    vin.Normal       = mul(vin.Normal, (float3x3) skinning);
}

// C O M P U T E   L I G H T S 
ColorPair ComputeLights(float3 eyeVector, float3 worldNormal, uniform int numLights) {
    float3x3 lightDirections = 0, lightDiffuse = 0, lightSpecular = 0, halfVectors = 0;    
    [unroll]
    for (int i = 0; i < numLights; i++) {
        lightDirections[i] = float3x3(DirLight0Direction, DirLight1Direction, DirLight2Direction)[i];
        lightDiffuse[i]    = float3x3(DirLight0DiffuseColor, DirLight1DiffuseColor, DirLight2DiffuseColor)[i];
        lightSpecular[i]   = float3x3(DirLight0SpecularColor, DirLight1SpecularColor, DirLight2SpecularColor)[i];        
        halfVectors[i]     = normalize(eyeVector - lightDirections[i]);
    }
    float3 dotL  = mul(-lightDirections, worldNormal);  // angle between light and surface (moreless)
    float3 dotH  = mul(halfVectors, worldNormal);    
    float3 zeroL = step(float3(0, 0, 0), dotL);         // clamp
    float3 diffuse  = zeroL * dotL;
    float3 specular = pow(max(dotH, 0) * zeroL, SpecularPower); // specular intensifier
    ColorPair result;    
    result.Diffuse  = mul(diffuse,  lightDiffuse)  * DiffuseColor.rgb + EmissiveColor;  // diffuse-factor * texture color (+emissive color)
    result.Specular = mul(specular, lightSpecular) * SpecularColor;                     // specular intensity * spec color
    return result;
}

// ---- COMMON VERTEX SHADER CALCULATIONS ----
// C A L C   C O M M O N   V S   O U T   W I T H   L I G H T (S)
CommonVSOut CalcCommonVSOutWithLights(float4 position, float3 normal, uniform int numLights) {
    CommonVSOut vout;    
    float4 world_pos   = mul(position, World);
    float3 eyeVector   = normalize(CamPos - world_pos.xyz);                   // vector pointing to camera from vertex
    float3 worldNormal = normalize(mul(normal, WorldInverseTranspose));       // direction of vertex-normal
    ColorPair lightResult = ComputeLights(eyeVector, worldNormal, numLights); // get lighting
    vout.Pos_PS    = mul(position, WorldViewProj);
    vout.Diffuse   = float4(lightResult.Diffuse, DiffuseColor.a);             // I guess diffuse color really only effects alpha (texture takes over)
    vout.Specular  = lightResult.Specular;
    vout.FogFactor = saturate(dot(position, FogVector));
    return vout;
}
// C A L C   C O M M O N   V S    P E R  P I X E L   L I G H T (S) 
CommonVSOutPerPixel CalcCommonVSOutPerPixelLights(float4 position, float3 normal)
{
    CommonVSOutPerPixel vout;    
    vout.Pos_PS = mul(position, WorldViewProj);
    vout.Pos_WS = mul(position, World).xyz;
    vout.Normal_WS = normalize(mul(normal, WorldInverseTranspose));
    vout.FogFactor = saturate(dot(position, FogVector));
    return vout;
}




// V E R T E X   S H A D E R   S T U F F _______________________________________________________________
// V S  _  V E R T  3 L I G H T S _  S K I N            (if using vertex lighting instead of pixel lighting)
VSOutputTx VS_Vert_Lights_Skin(VSInput_TxWeights vin)
{
    VSOutputTx vout;    
    int i;    
    Skin(vin);    
    CommonVSOut cout = CalcCommonVSOutWithLights(vin.Position, vin.Normal, 3);
    COMMON_VS_OUT_PARAMS;    
    vout.TexCoord = vin.TexCoord;
    return vout;
}
// V S  _  V E R T _ 1 L I G H T   _   S K I N 
VSOutputTx VS_Vert_1Light_Skin(VSInput_TxWeights vin)
{
    VSOutputTx vout;    
    Skin(vin);
    CommonVSOut cout = CalcCommonVSOutWithLights(vin.Position, vin.Normal, 1);
    COMMON_VS_OUT_PARAMS;    
    vout.TexCoord = vin.TexCoord;
    return vout;
}
// V S _ P E R  P I X _ 3 L I G H T _ S K I N 
VSOutputPerPixTx VS_PerPix_Lights_Skin(VSInput_TxWeights vin)
{
    VSOutputPerPixTx vout;    
    Skin(vin);
    CommonVSOutPerPixel cout = CalcCommonVSOutPerPixelLights(vin.Position, vin.Normal);
    COMMON_VS_OUT_PARAMS_PER_PIXEL;    
    vout.Diffuse  = float4(1, 1, 1, DiffuseColor.a);
    vout.TexCoord = vin.TexCoord;
    return vout;
}


// P I X E L   S H A D E R   S T U F F _________________________________________________________________
// P S _ V E R T _ L I T _ S K I N _ F O G
float4 PS_Vert_Lit_Skin_Fog(VSOutputTx pin) : SV_Target0
{
    float4 color = tex2D(texSampler, pin.TexCoord) * pin.Diffuse;
    color.rgb += pin.Specular * color.a;
    color.rgb = lerp(color.rgb, FogColor * color.a, pin.Specular.w);           
    return color;
}
// P S _ V E R T _ L I T _ S K I N 
float4 PS_Vert_Lit_Skin(VSOutputTx pin) : SV_Target0
{
    float4 color = tex2D(texSampler, pin.TexCoord) * pin.Diffuse;    
    color.rgb += pin.Specular;// * color.a;
    return color;
}
// P S _ P E R _ P I X E L _ L I T _ S K I N _ F O G
// NOTE: this has been modified with the assumption that transparent stuff will be like glass or ice - super shiny (see below)
float4 PS_Per_Pixel_Lit_Skin_Fog(VSOutputPerPixTx pin) : SV_Target0
{
    float4 color =  tex2D(texSampler, pin.TexCoord) * pin.Diffuse;    
    float3 eyeVector   = normalize(CamPos - pin.PositionWS.xyz);
    float3 worldNormal = normalize(pin.NormalWS);    
    ColorPair lightResult = ComputeLights(eyeVector, worldNormal, 3);    
    color.rgb *= lightResult.Diffuse;
    // ADDED THIS TO MAKE EYES LOOK REALLY SHINY: 
    if ((color.a < 0.8) && (lightResult.Specular.r > 0.8)) {
        color.rgb += lightResult.Specular; color.a = 1;
    }
    color.rgb += lightResult.Specular * shine_amplify * ((1 - color.a) * 100);  // <-- super-shiny control version (*100 adds a shine halo)    
    //color.rgb += lightResult.Specular * color.a;                              // <-- original version
    color.rgb = lerp(color.rgb, FogColor * color.a, pin.PositionWS.w);
    return color;
}



// T E C H N I Q U E S ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ 
#define TECHNIQUE(name, vsname, psname ) technique name { pass { VertexShader = compile VS_SHADERMODEL vsname (); PixelShader = compile PS_SHADERMODEL psname(); } }

TECHNIQUE(Skin_Vertex_Lights_Fog, VS_Vert_Lights_Skin, PS_Vert_Lit_Skin_Fog);  // VERTEX LIGHTING AND FOG (ALL LIGHTING)
TECHNIQUE(Skin_Vertex_Lights,     VS_Vert_Lights_Skin, PS_Vert_Lit_Skin);      // VERTEX LIGHTING (ALL LIGHTING) [no fog]

TECHNIQUE(Skin_Vertex_1Light_Fog, VS_Vert_1Light_Skin, PS_Vert_Lit_Skin_Fog);  // VERTEX 1 LIGHT AND FOG
TECHNIQUE(Skin_Vertex_1Light,     VS_Vert_1Light_Skin, PS_Vert_Lit_Skin);      // VERTEX 1 LIGHT [no fog]

TECHNIQUE(Skin_PerPixel_Lit_Fog, VS_PerPix_Lights_Skin, PS_Per_Pixel_Lit_Skin_Fog); // PER-PIXEL LIGHTING (ALL) AND FOG (Note: maybe switch to vertex lit if characters are far away)
TECHNIQUE(Skin_PerPixel_Lit,     VS_PerPix_Lights_Skin, PS_Per_Pixel_Lit_Skin_Fog); // PER-PIXEL LIGHTING                "                      " 