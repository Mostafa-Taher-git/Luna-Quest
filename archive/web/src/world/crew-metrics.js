/**
 * One set of human dimensions for the whole colony.
 *
 * Every human character in Luna-Quest is the same Cute Astronaut model. Keeping the height,
 * eye line and model path in one module stops the player, the six crew and the collision
 * capsules from drifting apart - a regression this project has hit before, when a rig bind
 * pose was measured instead of the skeleton and the crew rendered four times too tall.
 *
 * These are runtime dimensions only. The GLB itself is frozen; see
 * docs/design/Luna-Quest_Immutable_Assets_EN.md.
 */

/** Standing height of every astronaut, player and crew alike (metres). */
export const CREW_HEIGHT_M = 1.75;

/** The commander uses the same suit at the same scale as the crew. */
export const PLAYER_HEIGHT_M = CREW_HEIGHT_M;

/** Camera eye line inside that suit (metres). */
export const EYE_HEIGHT_M = 1.62;

/** Capsule radius used for movement and interaction (metres). */
export const BODY_RADIUS_M = 0.34;

/** Web-relative path of the one and only human character model. */
export const ASTRONAUT_MODEL_PATH = 'models/astronaut/cute_astronaut.glb';

/** Repository-relative path of the authoritative copy. */
export const ASTRONAUT_SOURCE_PATH = 'assets/astronaut/cute_astronaut.glb';

/** Pinned in docs/design/asset-lock.json. Never edit this without updating that file. */
export const ASTRONAUT_SHA256 = '86ae33158e9e7656410cde7c19db14ba8640ef808476afad4f50b4227a6841a2';
