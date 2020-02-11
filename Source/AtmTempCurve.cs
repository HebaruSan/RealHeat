﻿using System.IO;
using System.Threading;
using UnityEngine;

namespace RealHeat
{
    /// <summary>
    /// This class contains curves relating stagnation temperature to velocity
    /// and Cp to velocity
    /// (accounting for some real gas effects, like changes in specific heat and 
    /// dissocation), methods to develop the curve from atmospheric composition and
    /// the ability to dump the data in a comma delineated format
    /// </summary>
    public class AtmTempCurve
    {
        public FloatCurve tempAdditionFromVelocity = new FloatCurve();
        public CurveData[] protoTempCurve = null;
        public FloatCurve velCpCurve = new FloatCurve();
        public CurveData[] protoVelCpCurve = null;
        public float specificGasConstant = 287.103f;
        public float referenceTemp = 300;
        private static readonly object _locker = new object();
        public static bool recalculatingCurve = false;


        public void CalculateNewAtmTempCurve(CelestialBody body, bool dumpText)
        {
            if (recalculatingCurve)
                return;
            if (RealHeatUtils.multithreadedTempCurve)
                ThreadPool.QueueUserWorkItem(AtmDataOrganizer.CalculateNewTemperatureCurve, new tempCurveDataContainer(body, this, dumpText));
            else
                AtmDataOrganizer.CalculateNewTemperatureCurve(new tempCurveDataContainer(body, this, dumpText));
        }

        public float EvaluateTempDiffCurve(float vel)
        {
            if (protoTempCurve != null)
            {
                tempAdditionFromVelocity = new FloatCurve();
                Debug.Log("[RealHeat] Building Temperature Curve Object...");
                foreach(CurveData data in protoTempCurve)
                {
                    tempAdditionFromVelocity.Add(data.x, data.y, data.dy_dx, data.dy_dx);
                }
                protoTempCurve = null;
            }
            return tempAdditionFromVelocity.Evaluate(vel);
        }

        public float EvaluateVelCpCurve(float vel)
        {
            if (protoVelCpCurve != null)
            {
                velCpCurve = new FloatCurve();
                Debug.Log("[RealHeat] Building Cp Curve Object...");
                foreach (CurveData data in protoVelCpCurve)
                {
                    velCpCurve.Add(data.x, data.y, data.dy_dx, data.dy_dx);
                }
                protoVelCpCurve = null;
            }
            return velCpCurve.Evaluate(vel);
        }

        public void DumpToText(float velIncrements, CelestialBody body)
        {
            lock (_locker)
            {
                FileStream fs = File.Open(KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/RealHeat/" + body.bodyName + "_Curves.csv", FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);

                EvaluateTempDiffCurve(0f);
                EvaluateVelCpCurve(0f);
                for (float v = 0; v < tempAdditionFromVelocity.maxTime; v += velIncrements)
                {
                    float y = EvaluateTempDiffCurve(v) + referenceTemp;
                    float z = EvaluateVelCpCurve(v);
                    string s = v.ToString() + ", " + y.ToString() + ", " + z.ToString();
                    sw.WriteLine(s);
                }

                sw.Close();
                fs.Close();
            }
        }
    }
}
