using System;
using UnityEngine;

public class ObjectKey
{
    private int ObjectIndex;
    private int ObjectSerialNumber;

    public ObjectKey()
    {
        ObjectIndex = 0;
        ObjectSerialNumber = 0;
    }

    public ObjectKey(in UnityEngine.Object @object) : this()
    {
        if (@object != null)
        {
            var weak = new WeakReference(@object);
            // ObjectIndex = weak.GetObjectData()
            ObjectSerialNumber = 0; 
        }
    }
}
