using UnityEngine;
using System;  // Needed for Math

public class Sinus : MonoBehaviour
{
    public double frequency = 0;
    public double gain = 0.05;
    private double increment;
    private double phase;
    private double sampling_frequency = 48000;

    void OnAudioFilterRead(float[] data, int channels)
    {
        increment = frequency * 2 * Math.PI / sampling_frequency;

        for (var i = 0; i < data.Length; i = i + channels)
        {
            phase = phase + increment;
            data[i] = (float)(gain * Math.Sin(phase));
            if (channels == 2) data[i + 1] = data[i];
            if (phase > 2 * Math.PI) phase = 0;
        }
    }
    public void ReadComprateString(object data)
    {
        var sensor_data = data as string;
        double changedData = double.Parse(sensor_data);
        frequency = changedData;
    }
}

