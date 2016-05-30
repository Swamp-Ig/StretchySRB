﻿using KSPAPIExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ProceduralParts
{
    public class ProceduralShapePrism : ProceduralAbstractSoRShape
    {
        #region Properties (fields)

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Diameter", guiFormat = "F3", guiUnits = "m"),
         UI_FloatEdit(scene = UI_Scene.Editor, incrementSlide = 0.001f, sigFigs = 3, unit = "m", useSI = true)]
        public float diameter = 1.25f;
        private float oldDiameter;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Length", guiFormat = "F3", guiUnits = "m"),
         UI_FloatEdit(scene = UI_Scene.Editor, incrementSlide = 0.001f, sigFigs = 3, unit = "m", useSI = true)]
        public float length = 1f;
        private float oldLength;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Sides", guiFormat = "0"),
         UI_FloatEdit(scene = UI_Scene.Editor)]
        public float sides = 4f;
        private float oldSides;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Mode:"),
         UI_Toggle(disabledText = "Circumscribed", enabledText = "Inscribed")]
        public bool isInscribed = false;
        private bool oldIsInscribed;

        #endregion

        public override void OnStart(StartState state)
        {
            UpdateTechConstraints();
        }

        private static float GetRealOuterDiam(bool inscribed, float diam, int sides)
        {
            if (!inscribed)
                return diam;
            float theta = (Mathf.PI * 2f) / (float)sides;
            return diam / Mathf.Cos(theta / 2f);
        }

        private static float GetRealInnerDiam(bool inscribed, float diam, int sides)
        {
            if (inscribed)
                return diam;
            float theta = (Mathf.PI * 2f) / (float)sides;
            return diam * Mathf.Cos(theta / 2f);
        }

        private static float GetSideLength(bool inscribed, float diam, int sides)
        {
            float theta = (Mathf.PI * 2f) / (float)sides;
            float radius = diam / 2f;

            float tHeight = inscribed ? radius : radius * Mathf.Cos(theta / 2f);
            float tBase = 2f * tHeight * Mathf.Tan(theta / 2f);
            return tBase;
        }

        private float CalcVolume()
        {
            
            float theta = (Mathf.PI * 2f) / (float)sides;
            float radius = diameter / 2f;

            float tHeight = isInscribed ? radius : radius * Mathf.Cos(theta / 2f);
            float tBase = 2f * tHeight * Mathf.Tan(theta / 2f);

            return ((tHeight * tBase / 2f) * sides) * length;
        }

        private float MaxMinVolume()
        {
            Volume = CalcVolume();

            if (Volume > PPart.volumeMax)
            {
                float excess = Volume - PPart.volumeMax;
                Volume = PPart.volumeMax;
                return excess;
            }
            if (Volume < PPart.volumeMin)
            {
                float excess = Volume - PPart.volumeMin;
                Volume = PPart.volumeMin;
                return excess;
            }
            return 0;
        }

        protected override void UpdateShape(bool force)
        {
            if (!force &&
                oldDiameter == diameter &&
                oldLength == length &&
                oldSides == sides &&
                oldIsInscribed == isInscribed)
                return;

            Volume = CalcVolume();

            var sideLen = GetSideLength(isInscribed, diameter, (int)sides);
            RaiseChangeTextureScale("sides", PPart.SidesMaterial, new Vector2(sideLen * sides * 2f, length));

            //var outerDiam = GetRealOuterDiam(isInscribed, diameter, (int)sides);
            var innerDiam = GetRealInnerDiam(isInscribed, diameter, (int)sides);
            //var avgDiam = (innerDiam + outerDiam) / 2f;

            Vector2 norm = new Vector2(1, 0);
            UpdateMeshNodesSizes(
                new ProfilePoint(innerDiam, -0.5f * length, 0f, norm),
                new ProfilePoint(innerDiam, 0.5f * length, 1f, norm)
                );

            WriteMeshes(
                CreatePrismMesh((int)sides, diameter, length, isInscribed),
                CreatePrismEnds((int)sides, diameter, length, isInscribed),
                CreatePrismCollider((int)sides, diameter, length, isInscribed));

            oldDiameter = diameter;
            oldLength = length;
            oldSides = sides;
            oldIsInscribed = isInscribed;

            UpdateInterops();
        }

        #region Mesh Generation

        private static UncheckedMesh CreatePrismMesh(int sides, float diameter, float length, bool inscribed)
        {
            var realDiam = GetRealOuterDiam(inscribed, diameter, sides);
            float theta = (Mathf.PI * 2f) / (float)sides;

            var vertices = new List<Vertex>();
            var triangles = new List<int>();

            //vertices/normals/tangents/uvs
            for (int s = 0; s < sides; s++)
            {
                float posX = Mathf.Cos(theta * s);
                float posZ = -Mathf.Sin(theta * s);
                var norm = new Vector3(posX, 0, posZ);
                var curIndex = vertices.Count;
                var t1 = GetPrismVertex(theta * s - theta / 2f, realDiam, length, true, norm, new Vector2((float)s / (float)sides, 1f));
                var t2 = GetPrismVertex(theta * s + theta / 2f, realDiam, length, true, norm, new Vector2((float)(s + 1) / (float)sides, 1f));
                var b1 = GetPrismVertex(theta * s - theta / 2f, realDiam, length, false, norm, new Vector2((float)s / (float)sides, 0f));
                var b2 = GetPrismVertex(theta * s + theta / 2f, realDiam, length, false, norm, new Vector2((float)(s + 1) / (float)sides, 0f));
                vertices.AddRange(new Vertex[] { t1, t2, b1, b2 });
                triangles.AddRange(new int[]{
                    curIndex,//t1
                    curIndex + 1,//t2
                    curIndex + 2,//b1
                    curIndex + 2,//b1
                    curIndex + 1,//t2
                    curIndex + 3//b3
                });
            }
            var mesh = new UncheckedMesh(vertices.Count, triangles.Count / 3);
            for (int i = 0; i < vertices.Count; i++)
            {
                mesh.verticies[i] = vertices[i].Pos;
                mesh.normals[i] = vertices[i].Norm;
                mesh.tangents[i] = vertices[i].Tan;
                mesh.uv[i] = vertices[i].Uv;
            }
            for (int i = 0; i < triangles.Count; i++)
                mesh.triangles[i] = triangles[i];

            return mesh;
        }

        private static Vertex GetPrismVertex(float theta, float diameter, float length, bool top, Vector3 normal, Vector2 uv)
        {
            float posX = Mathf.Cos(theta);
            float posZ = -Mathf.Sin(theta);
            return new Vertex(
                new Vector3(posX * (diameter / 2f), (top ? -0.5f : 0.5f) * length, posZ * (diameter / 2f)),
                normal,
                GetTangentFromNormal(normal),
                uv);
        }

        private static Vector4 GetTangentFromNormal(Vector3 normal)
        {
            Vector3 t1 = Vector3.Cross(normal, Vector3.forward);
            Vector3 t2 = Vector3.Cross(normal, Vector3.up);
            return t1.magnitude > t2.magnitude ? t1 : t2;
        }

        private static UncheckedMesh CreatePrismEnds(int sides, float diameter, float length, bool inscribed)
        {
            var outerDiam = GetRealOuterDiam(inscribed, diameter, sides);
            var innerDiam = GetRealInnerDiam(inscribed, diameter, sides);
            
            float theta = (Mathf.PI * 2f) / (float)sides;
            theta /= 2f;
            var oRad = outerDiam / 2f;
            var iRad = innerDiam / 2f;

            var vertices = new List<Vertex>();

            vertices.Add(new Vertex(new Vector3(0, -0.5f * length, 0), new Vector3(0, -1, 0), new Vector4(-1, 0, 0, 1), new Vector2(0.5f, 0.5f)));//top center

            for (int s = 0; s <= sides * 2; s++)
            {
                float posX = Mathf.Cos(theta * s - theta);
                float posZ = -Mathf.Sin(theta * s - theta);
                var radius = s % 2 == 0 ? oRad : iRad;

                vertices.Add(new Vertex(
                    new Vector3(posX * radius, -0.5f * length, posZ * radius),
                    new Vector3(0, -1, 0),
                    new Vector4(-1, 0, 0, 1),
                    new Vector2((posX + 1f) / 2f, (posZ + 1f) / 2f)));
            }

            int vPerSide = vertices.Count;

            vertices.Add(new Vertex(new Vector3(0, 0.5f * length, 0), new Vector3(0, 1, 0), new Vector4(-1, 0, 0, -1), new Vector2(0.5f, 0.5f)));//bottom center

            for (int s = 0; s <= sides * 2; s++)
            {
                float posX = Mathf.Cos(theta * s - theta);
                float posZ = -Mathf.Sin(theta * s - theta);
                var radius = s % 2 == 0 ? oRad : iRad;

                vertices.Add(new Vertex(
                    new Vector3(posX * radius, 0.5f * length, posZ * radius),
                    new Vector3(0, 1, 0),
                    new Vector4(-1, 0, 0, -1),
                    new Vector2((posX + 1f) / 2f, (posZ + 1f) / 2f)));
            }
            
            var tList = new List<int>();

            for (int s = 0; s < sides * 2; s++)
            {
                tList.Add(0);
                tList.Add(2 + s);
                tList.Add(1 + s);

                tList.Add(vPerSide);
                tList.Add(vPerSide + 1 + s);
                tList.Add(vPerSide + 2 + s);
            }

            var mesh = new UncheckedMesh(vertices.Count, tList.Count / 3);

            for (int i = 0; i < vertices.Count; i++)
            {
                mesh.verticies[i] = vertices[i].Pos;
                mesh.normals[i] = vertices[i].Norm;
                mesh.tangents[i] = vertices[i].Tan;
                mesh.uv[i] = vertices[i].Uv;
            }

            for (int i = 0; i < tList.Count; i++)
                mesh.triangles[i] = tList[i];

            return mesh;
        }

        private static UncheckedMesh CreatePrismCollider(int sides, float diameter, float length, bool inscribed)
        {
            var outerDiam = GetRealOuterDiam(inscribed, diameter, sides);
            var radius = outerDiam / 2f;
            float theta = (Mathf.PI * 2f) / (float)sides;
            var alignOffset = theta / 2f;//angle offset so that the prism side face the camera


            var vertices = new List<Vertex>();

            vertices.Add(new Vertex(new Vector3(0, -0.5f * length, 0), new Vector3(0, -1, 0), new Vector4(-1, 0, 0, 1), new Vector2(0.5f, 0.5f)));//top center

            for (int s = 0; s < sides; s++)
            {
                float posX = Mathf.Cos(theta * s - alignOffset);
                float posZ = -Mathf.Sin(theta * s - alignOffset);
                vertices.Add(new Vertex(
                    new Vector3(posX * radius, -0.5f * length, posZ * radius),
                    new Vector3(posX, 0, posZ),
                    Vector4.zero,
                    new Vector2(0f, 0f)));
            }

            int vPerSide = vertices.Count;

            vertices.Add(new Vertex(new Vector3(0, 0.5f * length, 0), new Vector3(0, 1, 0), new Vector4(-1, 0, 0, -1), new Vector2(0.5f, 0.5f)));//bottom center

            for (int s = 0; s < sides; s++)
            {
                float posX = Mathf.Cos(theta * s - alignOffset);
                float posZ = -Mathf.Sin(theta * s - alignOffset);
                vertices.Add(new Vertex(
                    new Vector3(posX * radius, 0.5f * length, posZ * radius),
                    new Vector3(posX, 0, posZ),
                    Vector4.zero,
                    new Vector2(1f, 1f)));
            }
            var tList = new List<int>();

            int last = vertices.Count - 1;

            for (int s = 0; s < sides; s++)
            {
                //top cap
                tList.Add(0);
                tList.Add((s + 1) % sides + 1);
                tList.Add(s + 1);

                //side 1
                tList.Add(s + 1);
                tList.Add((s + 1) % sides + 1);
                tList.Add(vPerSide + s + 1);
                //side 2
                tList.Add(vPerSide + s + 1);
                tList.Add((s + 1) % sides + 1);
                tList.Add(vPerSide + (s + 1) % sides + 1);

                //bottom cap
                tList.Add(last);
                tList.Add(vPerSide + s + 1);
                tList.Add(vPerSide + (s + 1) % sides + 1);
                
            }

            var mesh = new UncheckedMesh(vertices.Count, tList.Count / 3);

            for (int i = 0; i < vertices.Count; i++)
            {
                mesh.verticies[i] = vertices[i].Pos;
                mesh.normals[i] = vertices[i].Norm;
                mesh.tangents[i] = vertices[i].Tan;
                mesh.uv[i] = vertices[i].Uv;
            }

            for (int i = 0; i < tList.Count; i++)
                mesh.triangles[i] = tList[i];

            return mesh;
        }

        class Vertex
        {
            public Vector3 Pos { get; set; }
            public Vector3 Norm { get; set; }
            public Vector4 Tan { get; set; }
            public Vector2 Uv { get; set; }

            public Vertex()
            {
                Pos = Vector3.zero;
                Norm = Vector3.zero;
                Tan = Vector4.zero;
                Uv = Vector2.zero;
            }

            public Vertex(Vector3 pos, Vector3 norm, Vector4 tan, Vector2 uv)
            {
                Pos = pos;
                Norm = norm;
                Tan = tan;
                Uv = uv;
            }
        }


        #endregion

        public override void UpdateTechConstraints()
        {
            if (!HighLogic.LoadedSceneIsEditor)
                return;

            if (PPart.lengthMin == PPart.lengthMax)
                Fields["length"].guiActiveEditor = false;
            else
            {
                UI_FloatEdit lengthEdit = (UI_FloatEdit)Fields["length"].uiControlEditor;
                lengthEdit.maxValue = PPart.lengthMax;
                lengthEdit.minValue = PPart.lengthMin;
                lengthEdit.incrementLarge = PPart.lengthLargeStep;
                lengthEdit.incrementSmall = PPart.lengthSmallStep;
                length = Mathf.Clamp(length, PPart.lengthMin, PPart.lengthMax);
            }

            if (PPart.diameterMin == PPart.diameterMax)
                Fields["diameter"].guiActiveEditor = false;
            else
            {
                UI_FloatEdit diameterEdit = (UI_FloatEdit)Fields["diameter"].uiControlEditor;
                if (null != diameterEdit)
                {
                    diameterEdit.maxValue = PPart.diameterMax;
                    diameterEdit.minValue = PPart.diameterMin;
                    diameterEdit.incrementLarge = PPart.diameterLargeStep;
                    diameterEdit.incrementSmall = PPart.diameterSmallStep;
                    diameter = Mathf.Clamp(diameter, PPart.diameterMin, PPart.diameterMax);
                }
                else
                    Debug.LogError("*PP* could not find field 'diameter'");
            }


            UI_FloatEdit sidesEdit = (UI_FloatEdit)Fields["sides"].uiControlEditor;
            sidesEdit.maxValue = 12f;
            sidesEdit.minValue = 3f;
            sidesEdit.incrementLarge = 1f;
            sidesEdit.incrementSmall = 0f;
            sidesEdit.incrementSlide = 1f;
            sides = Mathf.Clamp(sides, sidesEdit.minValue, sidesEdit.maxValue);

            //UI_Toggle polygonModeEdit = (UI_Toggle)Fields["isInscribed"].uiControlEditor;
            //isInscribed = polygonModeEdit.controlEnabled;
        }
        

        public override void UpdateTFInterops()
        {
            ProceduralPart.tfInterface.InvokeMember("AddInteropValue", ProceduralPart.tfBindingFlags, null, null, new System.Object[] { this.part, "diam1", diameter, "ProceduralParts" });
            ProceduralPart.tfInterface.InvokeMember("AddInteropValue", ProceduralPart.tfBindingFlags, null, null, new System.Object[] { this.part, "diam2", sides, "ProceduralParts" });
            ProceduralPart.tfInterface.InvokeMember("AddInteropValue", ProceduralPart.tfBindingFlags, null, null, new System.Object[] { this.part, "length", length, "ProceduralParts" });
        }
    }
}