layout(location = 0) in vec3 Position;
layout(location = 1) in vec3 TextureCoordinates;
layout(location = 2) in vec4 Color;
layout(location = 0) out vec3 fsin_pos;
layout(location = 1) out vec3 fsin_uv;
layout(location = 2) out vec4 fsin_color;

layout(set = 0, binding = 0) uniform Vars
{
    int blendMode;
    int screenScale;
    int floor3Dx;
    int floor3Dy;
    int floor3Dz;
    int floor3Dangle;
} vars;

vec2 SCREENSIZE = vec2(SCREEN_XSIZE * vars.screenScale, SCREEN_YSIZE * vars.screenScale);

mat4 glScale( float mx, float my, float mz ) {
  return mat4(  mx, 0.0, 0.0, 0.0,
               0.0,  my, 0.0, 0.0,
               0.0, 0.0,  mz, 0.0,
               0.0, 0.0, 0.0, 1.0 );
}

mat4 glOrtho( float left, float right, float bottom, float top, float nearVal, float farVal ) {
  float t_x = - (right + left) / (right - left);
  float t_y = - (top + bottom) / (top - bottom);
  float t_z = - (farVal + nearVal) / (farVal - nearVal);
  return mat4( 2.0 / right - left, 0.0, 0.0, t_x,
               0.0, 2.0 / top - bottom, 0.0, t_y,
               0.0, 0.0, -2.0 / farVal - nearVal, t_z,
               0.0, 0.0, 0.0, 1.0 );
}

mat4 glRotate( float angle, float x, float y, float z ) {
  float c = cos(angle);
  float s = sin(angle);
  return mat4( x*x*(1.0-c) + c, x*y*(1.0-c) - z*s, x*z*(1.0-c) + y*s, 0.0,
               y*x*(1.0-c) + z*s, y*y*(1.0-c) + c, y*z*(1.0-c) - x*s, 0.0,
               x*z*(1.0-c) - y*s, y*z*(1.0-c) + x*s, z*z*(1.0-c)+c, 0.0,
               0.0, 0.0, 0.0, 1.0);
}

mat4 glTranslate( float x, float y, float z ) {
  return mat4(1.0, 0.0, 0.0, x,
              0.0, 1.0, 0.0, y,
              0.0, 0.0, 1.0, z,
              0.0, 0.0, 0.0, 1.0);
}

mat4 CalcPerspective(float fov, float aspectRatio, float nearPlane, float farPlane)
{
    mat4 ret;
    float scaleY = 1.0 / tan(fov * 0.5);
    float scaleX = scaleY / aspectRatio;
    float negFarRange = farPlane / (nearPlane - farPlane);

    ret[0][0] = scaleX;
    ret[0][1] = 0;
    ret[0][2] = 0;
    ret[0][3] = 0;

    ret[1][0] = 0;
    ret[1][1] = scaleY;
    ret[1][2] = 0;
    ret[1][3] = 0;

    ret[2][0]  = 0;
    ret[2][1]  = 0;
    ret[2][2] = negFarRange;
    ret[2][3] = nearPlane * negFarRange;

    ret[3][0] = 0;
    ret[3][1] = 0;
    ret[3][2] = -1.0;
    ret[3][3] = 0;

    return ret;
}

mat4 identityMat = mat4(
    1, 0, 0, 0,
    0, 1, 0, 0,
    0, 0, 1, 0,
    0, 0, 0, 1
);

void main()
{
    vec4 finalPos = vec4(Position, 1);
    mat4 fullscreenProjection = identityMat
        * glScale(
            (1.0 / SCREEN_XSIZE) * 2,
            (1.0 / SCREEN_YSIZE) * 2,
            1
        )
    ;
    gl_Position = (fullscreenProjection * finalPos) - vec4(1, 1, 0, 0);
    fsin_pos = Position * vec3(1.0 / SCREEN_XSIZE, 1.0 / SCREEN_YSIZE, 1);
    fsin_uv = TextureCoordinates;
    fsin_color = Color;
}