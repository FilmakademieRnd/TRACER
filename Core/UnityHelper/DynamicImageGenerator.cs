using UnityEngine;

public static class DynamicImageGenerator
{
    //quick helper to generate byte image data for e.g. our scene browser
    public static byte[] GenerateAbstractLandscape32x32()
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        tex.filterMode = FilterMode.Point;

        Color[] pixels = new Color[size * size];

        // 1. Fill background with White (Sky)
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        // 2. Draw 1-2 simple Clouds in the upper sky (Black pixels)
        int cloudCount = Random.Range(1, 3);
        for (int c = 0; c < cloudCount; c++)
        {
            int cx = Random.Range(4, 26);
            int cy = Random.Range(22, 29);
            int cw = Random.Range(4, 8);

            for (int x = cx; x < Mathf.Min(size, cx + cw); x++)
            {
                pixels[cy * size + x] = Color.black;
                if (x > cx && x < cx + cw - 1 && cy + 1 < size)
                {
                    pixels[(cy + 1) * size + x] = Color.black;
                }
            }
        }

        // 3. Calculate Horizon / Hill height per column using a sine wave
        int[] groundHeight = new int[size];
        float freq = Random.Range(0.08f, 0.2f);
        float offset = Random.Range(0f, 10f);
        int baseHeight = Random.Range(6, 12);

        for (int x = 0; x < size; x++)
        {
            int h = Mathf.RoundToInt(baseHeight + Mathf.Sin(x * freq + offset) * 4f);
            groundHeight[x] = Mathf.Clamp(h, 2, 18);

            // Fill ground below horizon with Black
            for (int y = 0; y <= groundHeight[x]; y++)
            {
                pixels[y * size + x] = Color.black;
            }
        }

        // 4. Randomly add either a City Skyline (houses/towers) or Trees on top of the hill
        bool isCity = Random.value > 0.5f;
        int xPos = Random.Range(2, 5);

        while (xPos < size - 4)
        {
            if (isCity)
            {
                // Draw a rectangular building
                int bWidth = Random.Range(3, 6);
                int bHeight = Random.Range(4, 11);
                int baseY = groundHeight[xPos];

                for (int bx = xPos; bx < Mathf.Min(size, xPos + bWidth); bx++)
                {
                    for (int by = 0; by < bHeight; by++)
                    {
                        int py = baseY + by;
                        if (py < size)
                        {
                            // Leave tiny white windows inside the black silhouette
                            bool isWindow = (by % 2 == 1) && (bx > xPos) && (bx < xPos + bWidth - 1);
                            pixels[py * size + bx] = isWindow ? Color.white : Color.black;
                        }
                    }
                }
                xPos += bWidth + Random.Range(1, 4);
            }
            else
            {
                // Draw a simple pine tree
                int baseY = groundHeight[xPos];
                for (int ty = 1; ty <= 5; ty++)
                {
                    int py = baseY + ty;
                    if (py >= size) break;

                    pixels[py * size + xPos] = Color.black; // Trunk & center
                    if (ty >= 2 && ty <= 4) // Branches
                    {
                        if (xPos > 0) pixels[py * size + (xPos - 1)] = Color.black;
                        if (xPos < size - 1) pixels[py * size + (xPos + 1)] = Color.black;
                    }
                }
                xPos += Random.Range(4, 8);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        // Encode to PNG byte array and clean up the temporary Texture2D
        byte[] result = tex.EncodeToPNG();
        Object.Destroy(tex);

        return result;
    }
}