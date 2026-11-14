shader_type spatial;
// The Sun seen from the lunar surface: a small, hard-edged, blinding disc and a faint halo
// (the halo is camera bloom and dust on the visor; there is no atmosphere to scatter light).
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;

uniform vec3 core_color : source_color = vec3(1.0, 0.96, 0.86);
uniform float core_radius = 0.085;
uniform float halo_strength = 0.55;

void vertex() {
	// Billboard: face the camera, keep the node's translation.
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}

void fragment() {
	float d = length(UV - vec2(0.5)) * 2.0;
	float core = 1.0 - smoothstep(core_radius * 0.9, core_radius, d);
	float halo = exp(-d * 7.0) * halo_strength * (1.0 - smoothstep(0.7, 1.0, d));
	ALBEDO = core_color * (core * 4.0 + halo);
	ALPHA = 1.0;
}
