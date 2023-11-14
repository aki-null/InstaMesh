using System;
using UnityEngine;

namespace InstaMesh.Editor
{
    // Bool is not a blittable type. Seriously.
    [Serializable]
    public struct BurstBool
    {
        [SerializeField] private byte value;
        public static implicit operator BurstBool(bool v) => new() { value = Convert.ToByte(v) };
        public static implicit operator bool(BurstBool v) => v.value == 1;
    }
}