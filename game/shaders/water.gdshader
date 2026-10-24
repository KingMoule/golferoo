shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, specular_schlick_ggx;

uniform vec3 shallow_color : source_color = vec3(0.25, 0.60, 0.75);
uniform vec3 deep_color : source_color = vec3(0.12, 0.38, 0.58);
uniform float alpha = 0.78;

varying vec3 wpos;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	VERTEX.y += sin(wpos.x * 0.7 + TIME * 0.8) * cos(wpos.z * 0.6 + TIME * 0.6) * 0.04;
}

void fragment() {
	float w = sin(wpos.x * 0.9 + TIME * 0.5) * sin(wpos.z * 0.8 + TIME * 0.4);
	ALBEDO = mix(deep_color, shallow_color, w * 0.5 + 0.5);
	ALPHA = alpha;
	ROUGHNESS = 0.15;
}
