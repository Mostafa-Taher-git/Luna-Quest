shader_type spatial;
// Lunar regolith is not sand and not concrete. It is a vacuum-weathered, angular,
// glass-bearing dust with almost no atmosphere to soften the light. This shader keeps
// the surface matte, adds low-frequency terracing, and darkens the fine grain so the
// horizon does not read as a flat grey plane. No atmosphere, no fog, no wind ripples.
// v3: blends a photoscanned regolith set (Poly Haven "Moon Dusted 03", CC0) for close-range
// detail: albedo, normal and roughness are mapped in world XZ at two scales to hide tiling.

render_mode diffuse_burley, specular_schlick_ggx, cull_back;

uniform vec3 high_color : source_color = vec3(0.52, 0.50, 0.47);
uniform vec3 low_color : source_color = vec3(0.26, 0.25, 0.24);
uniform vec3 grain_color : source_color = vec3(0.16, 0.155, 0.15);
uniform float grain_strength : hint_range(0.0, 1.0) = 0.45;
uniform float crater_scale = 0.035;
uniform float terrace_strength : hint_range(0.0, 1.0) = 0.28;
uniform bool use_detail = false;
uniform sampler2D detail_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D detail_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D detail_rough : hint_roughness_g, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float detail_tile = 5.0;            // metres per texture repeat, close scale
uniform float detail_strength : hint_range(0.0, 1.0) = 0.6;
uniform float normal_strength : hint_range(0.0, 2.0) = 0.9;

varying vec3 world_pos;
varying vec3 world_nrm;

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

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_nrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
    vec2 w = world_pos.xz;
    float broad = fbm(w * crater_scale);
    float detail = fbm(w * 0.55 + vec2(17.0, 9.0));
    float grain = hash(floor(w * 38.0));

    // Broad albedo provinces (anorthosite highlands versus darker basalt).
    vec3 base = mix(low_color, high_color, smoothstep(0.28, 0.78, broad));
    float near = 1.0 - smoothstep(8.0, 48.0, length(VERTEX));
    float rough = 0.94;
    vec3 n_world = world_nrm;

    if (use_detail) {
        // Two scales, rotated against each other, so the 2k scan never visibly repeats.
        vec2 uv1 = w / detail_tile;
        vec2 uv2 = mat2(vec2(0.8, -0.6), vec2(0.6, 0.8)) * w / (detail_tile * 5.3) + vec2(0.37, 0.71);
        vec3 a1 = texture(detail_albedo, uv1).rgb;
        vec3 a2 = texture(detail_albedo, uv2).rgb;
        float lum = dot(mix(a2, a1, 0.6), vec3(0.299, 0.587, 0.114));
        // The scan supplies structure; the hand-tuned palette keeps the colony's grey.
        float fade = mix(0.3, 1.0, near);
        base *= mix(1.0, clamp(0.55 + 1.0 * lum, 0.6, 1.25), detail_strength * fade);
        vec3 t1 = texture(detail_normal, uv1).rgb * 2.0 - 1.0;
        vec3 t2 = texture(detail_normal, uv2).rgb * 2.0 - 1.0;
        // Normal detail fades out by 30 m: at grazing sun angles it would only shimmer.
        float nfade = 1.0 - smoothstep(6.0, 30.0, length(VERTEX));
        vec2 tn = (t1.xy * 0.65 + t2.xy * 0.35) * normal_strength * nfade;
        n_world = normalize(world_nrm + vec3(tn.x, 0.0, -tn.y));
        rough = mix(0.9, 1.0, texture(detail_rough, uv1).g);
    } else {
        // Pixel-level grain only close up: at distance it would shimmer.
        base = mix(base, grain_color, mix(0.25, grain, near) * grain_strength);
    }
    base *= 0.92 + 0.16 * detail;

    // A shallow terrace reads as compacted regolith without pretending to be a dune.
    float terrace = smoothstep(0.46, 0.54, fbm(w * 0.12));
    base *= 1.0 - terrace * terrace_strength;

    ALBEDO = base;
    NORMAL = normalize((VIEW_MATRIX * vec4(n_world, 0.0)).xyz);
    ROUGHNESS = rough;
    SPECULAR = 0.06;
    METALLIC = 0.0;
}
