#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0
    #define PS_SHADERMODEL ps_4_0
#endif

matrix ViewProjection;
float3 BillboardRight;
float3 BillboardUp;
float4 RotationCosA;
float2 RotationCosB;
float4 RotationSinA;
float2 RotationSinB;
float2 Perspective4D;
float PointScale;
// Maximum mass, speed, acceleration and absolute world W.
float4 Maxima;
float CameraWorldW;
float ColorMode;
float4 ColorLow;
float4 ColorHigh;
float4 SelectedColor;

struct VertexInput
{
    float2 Corner : POSITION0;
    // Translation is performed in double precision on CPU before the float upload.
    float4 RelativePosition4D : TEXCOORD0;
    // Signed radius (negative = selected), mass, speed, acceleration.
    float4 Data : TEXCOORD1;
};

struct VertexOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
};

float2 RotatePlane(float2 value, float cosine, float sine)
{
    return float2(cosine * value.x - sine * value.y,
                  sine * value.x + cosine * value.y);
}

VertexOutput MainVS(VertexInput input)
{
    VertexOutput output = (VertexOutput)0;
    float4 camera = input.RelativePosition4D;
    // Inverse camera orientation: negative angles in reverse ZW,YW,YZ,XW,XZ,XY order.
    camera.zw = RotatePlane(camera.zw, RotationCosB.y, -RotationSinB.y);
    camera.yw = RotatePlane(camera.yw, RotationCosB.x, -RotationSinB.x);
    camera.yz = RotatePlane(camera.yz, RotationCosA.w, -RotationSinA.w);
    camera.xw = RotatePlane(camera.xw, RotationCosA.z, -RotationSinA.z);
    camera.xz = RotatePlane(camera.xz, RotationCosA.y, -RotationSinA.y);
    camera.xy = RotatePlane(camera.xy, RotationCosA.x, -RotationSinA.x);

    // All four vertices of an invalid/behind-camera instance are clipped together.
    if (camera.w <= Perspective4D.y)
    {
        output.Position = float4(2, 2, 2, 1);
        return output;
    }

    float selected = input.Data.x < 0 ? 1 : 0;
    float radius = clamp(abs(input.Data.x) * 0.12 * PointScale, 0.004, 0.07);
    radius *= 1 + selected * 0.8;
    float3 center = camera.xyz * (Perspective4D.x / camera.w);
    float3 position = center + radius *
        (BillboardRight * input.Corner.x + BillboardUp * input.Corner.y);
    output.Position = mul(float4(position, 1), ViewProjection);

    // NBodyColorMode4D order: WDepth=0, Acceleration=1, Mass=2, Speed=3.
    float amount = ColorMode < 0.5
        ? 0.5 + 0.5 * (input.RelativePosition4D.w + CameraWorldW) / Maxima.w
        : ColorMode < 1.5
            ? input.Data.w / Maxima.z
            : ColorMode < 2.5
                ? sqrt(input.Data.y / Maxima.x)
                : input.Data.z / Maxima.y;
    output.Color = selected > 0.5 ? SelectedColor : lerp(ColorLow, ColorHigh, saturate(amount));
    return output;
}

float4 MainPS(VertexOutput input) : COLOR0
{
    return input.Color;
}

technique InstancedParticles
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
