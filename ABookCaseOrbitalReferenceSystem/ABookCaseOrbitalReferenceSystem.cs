﻿using System;
using UnityEngine;
using KSP.IO;
using System.Linq;
using System.Globalization;

namespace ABCORS
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal class ABookCaseOrbitalReferenceSystem : MonoBehaviour
    {
        private bool _mouseOver = false;
        private bool _isTarget = false;

        private Orbit _hitOrbit = null;
        private Vector3 _hitScreenPoint = new Vector3(0, 0, 0);
        private double _hitUT = 0;

        private Rect _popup = new Rect(0f, 0f, 160f, 160f);



        protected void Start()
        {
            _popup.Set(0, 0, HighLogic.CurrentGame.Parameters.CustomParams<ABCORSSettings>().displayWidth, HighLogic.CurrentGame.Parameters.CustomParams<ABCORSSettings>().displayWidth);
        }

        private void Awake()
        {
            _popup.center = new Vector2(Screen.width * 0.5f - _popup.width * 0.5f,
                Screen.height * 0.5f - _popup.height * 0.5f);
        }

        private void Update()
        {
            Update(FlightGlobals.ActiveVessel.mapObject);
        }

        protected void Update(MapObject mapObject)
        {
            if (!MapView.MapIsEnabled)
                return;

            _mouseOver = false;
            _isTarget = false;
            _hitOrbit = null;
            _hitUT = 0;

            if (mapObject.type == MapObject.ObjectType.Vessel)
            {
                Vessel vessel = mapObject.vessel;
                if (vessel != null && vessel.patchedConicSolver != null)
                {
                    if (MouseOverVessel(vessel))
                    {
                        _mouseOver = true;
                    }

                    // no hit on the main vessel, let's try the target
                    else if (HighLogic.CurrentGame.Parameters.CustomParams<ABCORSSettings>().allowTarget && vessel.targetObject != null)
                    {
                        Vessel targetVessel = vessel.targetObject as Vessel;
                        if (targetVessel != null)
                        {
                            if (MouseOverVessel(targetVessel))
                            {
                                _isTarget = true;
                                _mouseOver = true;
                            }
                        }
                        else
                        {
                            if (MouseOverTargetable(vessel.targetObject))
                            {
                                _isTarget = true;
                                _mouseOver = true;
                            }
                        }
                    }
                }
            }
            else if (mapObject.type == MapObject.ObjectType.CelestialBody)
            {
                if (MouseOverTargetable(mapObject.celestialBody))
                {
                    _mouseOver = true;
                }
            }

            if (_mouseOver)
            {
                _popup.center = new Vector2(_hitScreenPoint.x, Screen.height - _hitScreenPoint.y);
            }
        }

        private bool MouseOverVessel(Vessel vessel)
        {
            bool result = false;

            var patchRenderer = vessel.patchedConicRenderer;

            if (patchRenderer == null || patchRenderer.solver == null)
                return result;

            var patches = patchRenderer.solver.maneuverNodes.Any()
                ? patchRenderer.flightPlanRenders
                : patchRenderer.patchRenders;

            if (patches == null)
                return result;

            PatchedConics.PatchCastHit hit = default(PatchedConics.PatchCastHit);
            if (PatchedConics.ScreenCast(Input.mousePosition, patches, out hit))
            {
                result = true;
                _hitOrbit = hit.pr.patch;
                _hitScreenPoint = hit.GetScreenSpacePoint();
                _hitUT = hit.UTatTA;
            }

            return result;
        }

        private bool MouseOverTargetable(ITargetable targetable)
        {
            _isTarget = false;
            _hitOrbit = null;
            _hitUT = 0;

            bool result = false;

            OrbitDriver targetDriver = targetable.GetOrbitDriver();
            OrbitRenderer.OrbitCastHit rendererHit = default(OrbitRenderer.OrbitCastHit);
            if (targetDriver != null && targetDriver.Renderer.OrbitCast(Input.mousePosition, out rendererHit))
            {
                result = true;
                _hitOrbit = rendererHit.or.driver.orbit;
                _hitScreenPoint = rendererHit.GetScreenSpacePoint();
                _hitUT = rendererHit.UTatTA;
            }

            return result;
        }

        private void OnGUI()
        {
            if (!_mouseOver)
                return;

            GUI.skin = HighLogic.Skin;

            Orbit orbit = _hitOrbit;
            Vector3d deltaPos = orbit.getPositionAtUT(_hitUT) - orbit.referenceBody.position;
            double altitude = deltaPos.magnitude - orbit.referenceBody.Radius;
            double speed = orbit.getOrbitalSpeedAt(orbit.getObtAtUT(_hitUT));

            string labelText = "";
            if (HighLogic.CurrentGame.Parameters.CustomParams<ABCORSSettings>().showTime)
            {
                labelText += "T: " + KSPUtil.PrintTime((int)(Planetarium.GetUniversalTime() - _hitUT), 5, true) + "\n";
            }
            if (HighLogic.CurrentGame.Parameters.CustomParams<ABCORSSettings>().showAltitude)
            {
                labelText += "Alt: " + altitude.ToString("N0", CultureInfo.CurrentCulture) + "m\n";
            }
            if (HighLogic.CurrentGame.Parameters.CustomParams<ABCORSSettings>().showSpeed)
            {
                labelText += "Vel: " + speed.ToString("N0", CultureInfo.CurrentCulture) + "m/s\n";
            }
            if (HighLogic.CurrentGame.Parameters.CustomParams<ABCORSSettings>().showAngleToPrograde && orbit.referenceBody.orbit != null)
            {
                Vector3d bodyVel = orbit.referenceBody.orbit.getOrbitalVelocityAtUT(_hitUT);
                Vector3d shipPos = orbit.getRelativePositionAtUT(_hitUT);
                double angle = Vector3d.Angle(shipPos, bodyVel);
                Vector3d rotatedBodyVel = QuaternionD.AngleAxis(90.0, Vector3d.forward) * bodyVel;
                if (Vector3d.Dot(rotatedBodyVel, shipPos) > 0)
                {
                    angle = 360 - angle;
                }

                labelText += "\u03B1P: " + angle.ToString("N1", CultureInfo.CurrentCulture) + "\u00B0\n";
            }

            GUILayout.BeginArea(GUIUtility.ScreenToGUIRect(_popup));
            GUIStyle labelStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            if (_isTarget)
                labelStyle.normal.textColor = Color.cyan;
            GUILayout.Label(labelText, labelStyle);
            GUILayout.EndArea();
        }
    }

    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    internal class ABookCaseOrbitalReferenceSystemTS : ABookCaseOrbitalReferenceSystem
    {
        // TODO: use GameEvents.onPlanetariumTargetChanged
        private void Update()
        {
            Update(PlanetariumCamera.fetch.target);
        }
    }
}
