using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.Events;

namespace Tools
{
    static class GizmoTools
    {
        public static void DrawWireDisc(Vector3 center, Vector3 normal, float radius)
        {
            int segments = 32;
            Vector3 previousPoint = center + (Vector3.forward * radius);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * 2 * Mathf.PI / segments;
                Vector3 newPoint = center + new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius);
                Gizmos.DrawLine(previousPoint, newPoint);
                previousPoint = newPoint;
            }
        }
    }

    static class SystemTools
    {
        public static void Shuffle<T>(this System.Random rng, T[] array)
        {
            int n = array.Length;
            while (n > 1)
            {
                int k = rng.Next(n--);
                T temp = array[n];
                array[n] = array[k];
                array[k] = temp;
            }
        }

        //completely undermines the whole point of a queue. but i've already come this far
        public static void PlaceAtFirst<T>(this Queue<T> queue, T newFirstItem)
        {
            var items = queue.ToArray();
            queue.Clear();
            queue.Enqueue(newFirstItem);
            foreach (var item in items)
                queue.Enqueue(item);
        }

        //completely undermines the whole point of a queue. but i've already come this far
        public static void Move<T>(this List<T> list, int oldIndex, int newIndex)
        {
            T item = list[oldIndex];
            list.RemoveAt(oldIndex);
            list.Insert(newIndex, item);
        }

        public static T Clone<T>(T source)
        {
            if (!typeof(T).IsSerializable)
            {
                throw new ArgumentException("The type must be serializable.", nameof(source));
            }

            // Don't serialize a null object, simply return the default for that object
            if (ReferenceEquals(source, null)) return default;

            using var stream = new MemoryStream();
            IFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, source);
            stream.Seek(0, SeekOrigin.Begin);
            return (T)formatter.Deserialize(stream);
        }
    }

    static class UITools
    {
        public static void AnimateText(string text, ref string current, ref DateTime TimeFromLast, float rate, out bool animating)
        {
            animating = true;
            TimeSpan TimeBetween;

            TimeBetween = TimeFromLast - DateTime.Now;

            if ((TimeBetween.Milliseconds / 100) > rate)
            {
                // next letter

                if (text.Length != current.Length)
                {
                    current.Append<char>(text.ElementAt(current.Length + 1));
                }
                else
                {
                    animating = false;
                }

                TimeFromLast = DateTime.Now;
            }
        }
    }
}

public class WaitForFrames : CustomYieldInstruction
{
    private int _targetFrameCount;

    public WaitForFrames(int numberOfFrames)
    {
        _targetFrameCount = Time.frameCount + numberOfFrames;
    }

    public override bool keepWaiting
    {
        get
        {
            return Time.frameCount < _targetFrameCount;
        }
    }
}