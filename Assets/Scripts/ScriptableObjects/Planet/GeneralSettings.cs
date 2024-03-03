using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable, CreateAssetMenu(menuName = "Planet Settings/General Settings")]
public class GeneralSettings : GeneralSettingsMaster 
{
    [SerializeField]
    public float radius = 1000f;
    [SerializeField]
    public float distance;
    [SerializeField]
    public float distanceDivideFactor = 2f;
    [SerializeField]
    public int maxTreeDepth = 4;
    [Range(2, 255), SerializeField]
    public int res = 25;
    
    public Material mat;
}

[System.Serializable, CreateAssetMenu(menuName = "Planet Settings/General Settings Master")]
public class GeneralSettingsMaster : ScriptableObject
{
    
}