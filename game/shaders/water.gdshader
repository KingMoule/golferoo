shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, specular_schlick_ggx;

#include "res://shaders/surface_field.gdshaderinc"

uniform vec3 shallow_color : source_color = vec3(0.25, 0.60, 0.75);
uniform vec3 deep_color : source_color = vec3(0.12, 0.38, 0.58);
uniform float alpha = 0.78;
// Plans d'eau : le maillage déborde d'une cellule et le champ des surfaces trace la rive (la même que celle du terrain) ;
// l'eau s'éclaircit sur ses premiers mètres, comme sur une rive en pente douce. Sans masque (flaques), le carré entier.
uniform bool shore_mask = false;
uniform float shallow_meters = 1.5;

varying vec3 wpos;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	VERTEX.y += sin(wpos.x * 0.7 + TIME * 0.8) * cos(wpos.z * 0.6 + TIME * 0.6) * 0.04;
}

void fragment() {
	float w = sin(wpos.x * 0.9 + TIME * 0.5) * sin(wpos.z * 0.8 + TIME * 0.4);
	ALBEDO = mix(deep_color, shallow_color, w * 0.5 + 0.5);
	float a = alpha;
	if (shore_mask) {
		int best;
		int second;
		float dist;
		surface_pick(wpos.xz, best, second, dist);
		float inside = best == 5 ? dist : -dist;
		a *= smoothstep(-0.1, shallow_meters, inside);
		if (a < 0.01) {
			discard;
		}
	}
	ALPHA = a;
	ROUGHNESS = 0.15;
}
