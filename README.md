# monono2
Aion geo viewer and geo builder.
Based on GeoDataBuilderJ.

## AionClientViewer.exe
Viewer for the Aion client directory and .pak contents, encrypted files, images, 3D models and levels.

3D viewer keyboard controls:<br>
1 - Toggle terrain.<br>
2 - Toggle non-collidable meshes.<br>
3 - Toggle collidable meshes.<br>
4 - Toggle origin/axis lines.<br>
5 - Toggle floor/navmesh. (requires code modification to use).<br>
6 - Toggle names.<br>
7 - Toggle doors: state 1/state 2/hidden.<br>
WADS - move.<br>
RF - move up/down.<br>
Shift - speed up.<br>

## ALGeoBuilder
Geodata is generated in a format similar to the typical format used by Aion Lightning servers
but should be expected to require code modification to use.
- Loads terrain, brushes and vegetation.
- Generates a custom door format.
- Generates a custom navmesh format.

## monono2.exe
Native Monogame (non-winforms) level viewer.<br>
- Usage: monono2.exe (client dir) (level name)
