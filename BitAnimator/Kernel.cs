
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using UnityEngine;

public class Kernel
{
    public int ID;
    public Vector3Int size;
    public string name;
    public Kernel(ComputeShader shader, string name)
    {
        this.name = name;
        ID = shader.FindKernel(name);
        uint x, y, z;
        shader.GetKernelThreadGroupSizes(ID, out x, out y, out z);
        size.x = (int)x;
        size.y = (int)y;
        size.z = (int)z;
    }
    public override string ToString()
    {
        return name + " groupSize=" + size.ToString();
    }
}
public static class ComputeProgramUtils
{
    public static void InitializeBuffer(ref ComputeBuffer buffer, int newCount, int stride)
    {
        if (buffer == null)
            buffer = new ComputeBuffer(newCount, stride);
        else if (buffer.count < newCount)
        {
            buffer.Release();
            buffer = new ComputeBuffer(newCount, stride);
        }
    }
    public static void DispatchGrid(this ComputeShader shader, Kernel kernel, int gridSizeX, int gridSizeY, int gridSizeZ)
    {
        int X = (gridSizeX - 1) / kernel.size.x + 1;
        int Y = (gridSizeY - 1) / kernel.size.y + 1;
        int Z = (gridSizeZ - 1) / kernel.size.z + 1;
        shader.Dispatch(kernel.ID, X, Y, Z);
    }
    public static void DispatchGrid(this ComputeShader shader, Kernel kernel, int gridSizeX, int gridSizeY)
    {
        int X = (gridSizeX - 1) / kernel.size.x + 1;
        int Y = (gridSizeY - 1) / kernel.size.y + 1;
        shader.Dispatch(kernel.ID, X, Y, 1);
    }
    public static void DispatchGrid(this ComputeShader shader, Kernel kernel, int gridSizeX)
    {
        int X = (gridSizeX - 1) / kernel.size.x + 1;
        shader.Dispatch(kernel.ID, X, 1, 1);
    }
}