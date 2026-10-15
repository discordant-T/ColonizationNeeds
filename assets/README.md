# Carrier application icon

`ColonizationNeeds.png` is the square transparent master. `ColonizationNeeds.ico` packages 16, 24, 32, 48, 64, 128 and 256 pixel versions. The icon is embedded in the executable by build.ps1 and ColonizationNeeds.csproj; the main form reads the embedded executable icon for the window/taskbar. Users only need the executable.

Source: the user-supplied [white Javelin-Class Carrier image on the Elite Dangerous Wiki](https://static.wikia.nocookie.net/elite-dangerous/images/f/fe/Javelin-Class_Carrier_White_ED.jpg/revision/latest?cb=20251014100351), retrieved October 14, 2026. The carrier artwork belongs to its original rights holders; inclusion here does not claim ownership of that artwork.

Prepared using the built-in imagegen tool with this prompt:

> Use case: background-extraction. Asset type: Windows application icon, square. Edit target: supplied white Javelin-Class Carrier screenshot. Make a faithful isolated cutout of this exact carrier with a genuinely transparent background. Keep recognizable white hull, blue-gray landing decks and structure and its current perspective. Frame the entire carrier centered diagonally from lower left to upper right, filling a square canvas with only a small safe margin. Increase hull clarity and simplify only tiny surface details enough for a readable desktop icon. No stars, no scenery, no text, no border, no badge, no added objects. Preserve the ship identity and proportions.

The resulting PNG is converted to ICO with Pillow (asset preparation only; building/running the app requires no Python packages):

```python
from PIL import Image
im = Image.open('assets/ColonizationNeeds.png').convert('RGBA')
im.save('assets/ColonizationNeeds.ico', sizes=[(n,n) for n in (16,24,32,48,64,128,256)])
```
