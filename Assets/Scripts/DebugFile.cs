using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


public class DebugFile : Singleton<DebugFile>
{
    private string customPath;
    private bool addedFunc = false;
    private void Start()
    {
        DontDestroyOnLoad(this);
        customPath = Application.dataPath + "/PlayTimings.txt";
        //Create the file if it doesn't exist
        if (!File.Exists(customPath))
        {
            File.Create(customPath).Close();
        }
    }
    
    private void OnEnable()
    {
        if (!addedFunc)
        {
            addedFunc = true;    
        }
        Application.logMessageReceived += Log;
    }
    
    private void OnApplicationQuit()
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