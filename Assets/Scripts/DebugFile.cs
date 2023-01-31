using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DebugFile : MonoBehaviour
{
    private string customPath;
    
    private void Start()
    {
        customPath = Application.dataPath + "/PlayTimings.txt";
        //Create the file if it doesn't exist
        if (!File.Exists(customPath))
        {
            File.Create(customPath).Close();
        }
    }
    
    private void OnEnable()
    {
        Application.logMessageReceived += Log;
        Debug.Log("       [" + System.DateTime.Now + "]       ");
    }
    
    private void OnDisable()
    {
        Application.logMessageReceived -= Log;
    }
    
    public void Log(string logString, string stackTrace, LogType logType)
    {
        if (logType == LogType.Log)
        {
            TextWriter tw = new StreamWriter(customPath, true);
            tw.WriteLine(logString);
            tw.Close();    
        }
            
    }
}