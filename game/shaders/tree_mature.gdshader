shader_type spatial;
render_mode world_vertex_coords, cull_disabled;

// Arbres matures (C24 lot 4) : écorce et feuillage texturés, feuilles découpées par un masque alpha (seuil de glTF),
// deux faces visibles, mêmes points focaux et même vent que tree.gdshader. Toujours verts : pas de couleur de saison.
#include "res://shaders/tree_common.gdshaderinc"

uniform vec4 albedo : source_color = vec4(1.0);
uniform sampler2D albedo_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform bool use_texture = false;
uniform sampler2D normal_texture : hint_normal, filter_linear_mipmap, repeat_enable;
uniform bool use_normal = false;
uniform float alpha_cut = 0.0;
uniform float roughness_value = 1.0;

void vertex() {
	float h = max(VERTEX.y - MODEL_MATRIX[3].y - TRUNK_HEIGHT, 0.0) / 14.0;
	vec3 foot = MODEL_MATRIX[3].xyz;
	float sway = 0.6 + 0.4 * sin(TIME * 1.3 + foot.x * 0.21 + foot.z * 0.17);
	VERTEX.xz += tree_wind.xz * 0.04 * h * h * sway;
}

void fragment() {
	float above_base = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).y - MODEL_MATRIX[3].y;
	if (above_base > TRUNK_HEIGHT) {
		float fade = max(
			focus_fade(tree_focus_a, VERTEX, SCREEN_UV, VIEW_MATRIX, PROJECTION_MATRIX, VIEWPORT_SIZE),
			focus_fade(tree_focus_b, VERTEX, SCREEN_UV, VIEW_MATRIX, PROJECTION_MATRIX, VIEWPORT_SIZE));
		if (min(fade, 0.75) > bayer4(FRAGCOORD.xy)) {
			discard;
		}
	}
	vec4 colour = albedo;
	if (use_texture) {
		colour *= texture(albedo_texture, UV);
	}
	ALBEDO = colour.rgb;
	ALPHA = colour.a;
	ALPHA_SCISSOR_THRESHOLD = alpha_cut;
	ROUGHNESS = roughness_value;
	if (use_normal) {
		NORMAL_MAP = texture(normal_texture, UV).rgb;
	}
}
