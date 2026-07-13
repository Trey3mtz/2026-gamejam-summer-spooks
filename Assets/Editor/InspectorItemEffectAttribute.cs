using UnityEngine;
using System;

namespace SpookyGame 
{
    // Apply this to your [SerializeReference] fields to generate a dropdown
    [AttributeUsage(AttributeTargets.Field)]
    public class SubclassPickerAttribute : PropertyAttribute { }
}