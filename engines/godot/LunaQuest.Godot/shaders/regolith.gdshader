shader_type spatial;
// Lunar regolith is not sand and not concrete. It is a vacuum-weathered, angular,
// glass-bearing dust with almost no atmosphere to soften the light. This shader keeps
// the surface matte, adds low-frequency terracing, and darkens the fine grain so the
// horizon does not read as a flat grey plane. No atmosphere, no fog, no wind ripples.

render_mode diffuse_burley, specular_schlick_ggx, cull_back;

uniform vec3 high_color : source_color = vec3(0.52, 0.50, 0.47);
uniform vec3 low_color : source_color = vec3(0.26, 0.25, 0.24);
uniform vec3 grain_color : source_color = vec3(0.16, 0.155, 0.15);
uniform float grain_strength : hint_range(0.0, 1.0) = 0.45;
uniform float crater_scale = 0.035;
uniform float terrace_strength : hint_range(0.0, 1.0) = 0.28;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
               mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float fbm(vec2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++) {
        v += a * noise(p);
        p *= 2.03;
        a *= 0.5;
    }
    return v;
}

void fragment() {
    // World-space XZ so the pattern does not stretch with mesh scale.
    vec2 w = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xz;

    float broad = fbm(w * crater_scale);
    float detail = fbm(w * 0.55 + vec2(17.0, 9.0));
    float grain = hash(floor(w * 38.0));

    // Broad albedo provinces (anorthosite highlands versus darker basalt).
    vec3 base = mix(low_color, high_color, smoothstep(0.28, 0.78, broad));
    // Pixel-level grain only close up: at distance it would shimmer, so it fades to its average.
    float near = 1.0 - smoothstep(8.0, 48.0, length(VERTEX));
    base = mix(base, grain_color, mix(0.25, grain, near) * grain_strength);
    base *= 0.92 + 0.16 * detail;

    // A shallow terrace reads as compacted regolith without pretending to be a dune.
    float terrace = smoothstep(0.46, 0.54, fbm(w * 0.12));
    base *= 1.0 - terrace * terrace_strength;

    ALBEDO = base;
    ROUGHNESS = 0.94;
    SPECULAR = 0.06;
    METALLIC = 0.0;
}
