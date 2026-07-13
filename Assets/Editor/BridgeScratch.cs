using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using Escult.ProcGen;

/// <summary>
/// Scratch pad for Unity Bridge one-off scripts.
/// Edit Run() and execute via: {"type": "scratch"}
/// </summary>
public static class BridgeScratch
{
    public static string Run()
    {
        return Escult.ProcGen.EscultCli.CheckAll();
    }
}
