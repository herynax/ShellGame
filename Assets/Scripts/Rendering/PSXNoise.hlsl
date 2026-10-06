#ifndef SHELLGAME_PSX_NOISE_INCLUDED
#define SHELLGAME_PSX_NOISE_INCLUDED

// Простой value-noise на хэше, без текстур. Общий для всех полноэкранных
// пассов ShellGame (Wobble, OldScreen), чтобы не дублировать реализацию.

float PSXHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float PSXValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = PSXHash21(i);
    float b = PSXHash21(i + float2(1, 0));
    float c = PSXHash21(i + float2(0, 1));
    float d = PSXHash21(i + float2(1, 1));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

#endif