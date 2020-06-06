using System;
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
        private bool _isTarget;

        private Orbit _hitOrbit;
        private Vector3 _hitScreenPoint;
        private double _hitUT;

        private Rect _popup = new Rect(0f, 0f, 160f, 160f);


        protected void Start()
        {
            _popup.Set(0, 0, HighLogic.CurrentGame.Parameters.CustomParams<ABCORSSettings>().displayWidth, HighLogic.CurrentGame.Parameters.CustomParams<ABCORSSettings>().displayWidth);
        }

        private void Update()
        {
            Update(FlightGlobals.ActiveVessel?.mapObject);
        }

        protected void Update(MapObject mapObject)
        {
            _mouseOver = false;

            if (mapObject == null || !MapView.MapIsEnabled)
                return;

            if (mapObject.type == MapObject.ObjectType.Vessel)
            {
                Vessel vessel = mapObject.vessel;
                if (vessel != null && vessel.patchedConicSolver != null)
                {
                    if (MouseOverVessel(vessel))
                    {
                        _isTarget = false;
                        _mouseOver = true;
                    }

                    // no hit on the main vessel, let's try the target
                    else if (HighLogic.CurrentGame.Parameters.CustomParams<ABCORSSettings>().allowTarget)
                    {
                        if (MouseOverVessel(vessel.targetObject as Vessel) || MouseOverTargetable(vessel.targetObject))
                        {
                            _isTarget = true;
                            _mouseOver = true;
                        }
                    }
                }
            }

            else if (mapObject.type == MapObject.ObjectType.CelestialBody && mapObject.celestialBody != null)
            {
                if (MouseOverTargetable(mapObject.celestialBody))
                {
                    _isTarget = false;
                    _mouseOver = true;
                }
            }
        }

        private bool MouseOverVessel(Vessel vessel)
        {
            if (vessel == null)
                return false;

            var patchRenderer = vessel.patchedConicRenderer;

            if (patchRenderer?.solver == null)
                return false;

            var patches = patchRenderer.solver.maneuverNodes.Any()
                ? patchRenderer.flightPlanRenders
                : patchRenderer.patchRenders;

            if (patches == null)
                return false;

            PatchedConics.PatchCastHit hit = default(PatchedConics.PatchCastHit);
            if (!PatchedConics.ScreenCast(Input.mousePosition, patches, out hit))
                return false;

            _hitOrbit = hit.pr.patch;
            _hitScreenPoint = hit.GetScreenSpacePoint();
            _hitUT = hit.UTatTA;

            return true;
        }

        private bool MouseOverTargetable(ITargetable target)
        {
            if (target == null)
                return false;

            OrbitDriver orbitDriver = target.GetOrbitDriver();

            // do not look directly into the sun
            if (orbitDriver?.Renderer == null)
                return false;

            OrbitRenderer.OrbitCastHit hit = default(OrbitRenderer.OrbitCastHit);
            if (!orbitDriver.Renderer.OrbitCast(Input.mousePosition, out hit))
                return false;

            _hitOrbit = hit.or.driver.orbit;
            _hitScreenPoint = hit.GetScreenSpacePoint();
            _hitUT = hit.UTatTA;

            return true;
        }

        private void OnGUI()
        {
            if (!_mouseOver)
                return;

            GUI.skin = HighLogic.Skin;

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            if (_isTarget)
                labelStyle.normal.textColor = Color.cyan;

            Orbit orbit = _hitOrbit;
            Vector3d deltaPos = orbit.getPositionAtUT(_hitUT) - orbit.referenceBody.position;
            double altitude = deltaPos.magnitude - orbit.referenceBody.Radius;
            double speed = orbit.getOrbitalSpeedAt(orbit.getObtAtUT(_hitUT));

            string labelText = "";
            if (HighLogic.CurrentGame.Parameters.CustomParams<ABCORSSettings>().showTime)
            {
                labelText += "T: " + KSPUtil.PrintTime((Planetarium.GetUniversalTime() - _hitUT), 5, true) + "\n";
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

            _popup.center = new Vector2(_hitScreenPoint.x, Screen.height - _hitScreenPoint.y);
            GUILayout.BeginArea(GUIUtility.ScreenToGUIRect(_popup));
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
            Update(PlanetariumCamera.fetch?.target);
        }
    }
}
