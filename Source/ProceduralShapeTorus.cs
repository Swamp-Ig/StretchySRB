using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ProceduralParts
{
    class ProceduralShapeTorus : ProceduralAbstractShape
    {
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Inner D", guiFormat = "F3", guiUnits = "m", groupName = ProceduralPart.PAWGroupName),
            UI_FloatEdit(scene = UI_Scene.Editor, incrementSlide = SliderPrecision, sigFigs = 5, unit = "m", useSI = true)]
        public float innerDiameter = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Outer D", guiFormat = "F3", guiUnits = "m", groupName = ProceduralPart.PAWGroupName),
            UI_FloatEdit(scene = UI_Scene.Editor, incrementSlide = SliderPrecision, sigFigs = 5, unit = "m", useSI = true)]
        public float outerDiameter = 2f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Sides", guiFormat = "N0", groupName = ProceduralPart.PAWGroupName)]
        [UI_FloatRange(scene = UI_Scene.Editor, minValue = 3, maxValue = 30, stepIncrement = 1)]
        public float numSides = 18;

        public float Length => MinorRadius * 2;
        public float MajorRadius => (outerDiameter + innerDiameter) / 2;
        public float MinorRadius => (outerDiameter - innerDiameter) / 2;

        [KSPField]
        public string TopNodeName = "top";

        [KSPField]
        public string BottomNodeName = "bottom";

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (HighLogic.LoadedSceneIsEditor)
            {
                UpdateTechConstraints();
                Fields[nameof(innerDiameter)].uiControlEditor.onFieldChanged = OnShapeDimensionChanged;
                Fields[nameof(outerDiameter)].uiControlEditor.onFieldChanged = OnShapeDimensionChanged;
                Fields[nameof(numSides)].uiControlEditor.onFieldChanged = OnShapeDimensionChanged;
                PPart.Fields[nameof(PPart.capTextureIndex)].guiActiveEditor = false;
            }
        }

        public override void AdjustDimensionBounds()
        {
            float maxOuterDiameter = PPart.diameterMax;
            float maxInnerDiameter = PPart.diameterMax;
            float minOuterDiameter = PPart.diameterMin;
            float minInnerDiameter = PPart.diameterMin;

            // Vary the outer diameter to stay within min and max volume, given inner diameter
            if (PPart.volumeMax < float.PositiveInfinity)
            {
                var majorRadMax = PPart.volumeMax / (Mathf.PI * MinorRadius * MinorRadius * 2 * Mathf.PI);
                var minorRadMax = Mathf.Sqrt(PPart.volumeMax / (Mathf.PI * MajorRadius * 2 * Mathf.PI));

                //MajorRadius => (outerDiameter + innerDiameter) / 2
                //MinorRadius => (outerDiameter - innerDiameter) / 2;
                maxOuterDiameter = majorRadMax * 2 - innerDiameter;
                maxInnerDiameter = -(minorRadMax * 2 - outerDiameter);
            }

            maxOuterDiameter = Mathf.Clamp(maxOuterDiameter, PPart.diameterMin, PPart.diameterMax);
            maxInnerDiameter = Mathf.Clamp(maxInnerDiameter, PPart.diameterMin, PPart.diameterMax);
            maxInnerDiameter = Mathf.Clamp(maxInnerDiameter, PPart.diameterMin, outerDiameter - PPart.diameterSmallStep);

            minOuterDiameter = Mathf.Clamp(minOuterDiameter, innerDiameter + PPart.diameterSmallStep, maxOuterDiameter);

            (Fields[nameof(outerDiameter)].uiControlEditor as UI_FloatEdit).maxValue = maxOuterDiameter;
            (Fields[nameof(outerDiameter)].uiControlEditor as UI_FloatEdit).minValue = minOuterDiameter;
            (Fields[nameof(innerDiameter)].uiControlEditor as UI_FloatEdit).maxValue = maxInnerDiameter;
            (Fields[nameof(innerDiameter)].uiControlEditor as UI_FloatEdit).minValue = minInnerDiameter;

        }

        public override float CalculateVolume()
        {
            // Volume of a torus = (pi * minorRadius^2) * (2 * pi * majorRadius)
            return Mathf.PI * MinorRadius * MinorRadius * 2 * Mathf.PI * MajorRadius;
        }

        public override void NormalizeCylindricCoordinates(ShapeCoordinates coords)
        {
            throw new NotImplementedException();
        }

        public override bool SeekVolume(float targetVolume, int dir = 0)
        {
            // This is ugly.
            var orig = outerDiameter;
            var minorRadMax = Mathf.Sqrt(targetVolume / (Mathf.PI * MajorRadius * 2 * Mathf.PI));
            var newOuterDiam = minorRadMax * 2 + innerDiameter;
            var field = Fields[nameof(outerDiameter)];
            float precision = (field.uiControlEditor as UI_FloatEdit).incrementSlide;
            newOuterDiam = RoundToDirection(newOuterDiam / precision, dir) * precision;
            float clampedScaledValue = Mathf.Clamp(newOuterDiam, innerDiameter, PPart.diameterMax);
            bool closeEnough = Mathf.Abs((clampedScaledValue / newOuterDiam) - 1) < 0.01;
            field.SetValue(clampedScaledValue, this);
            foreach (Part p in part.symmetryCounterparts)
            {
                // Propagate the change to other parts in symmetry group
                if (FindAbstractShapeModule(p, this) is ProceduralAbstractShape pm)
                {
                    field.SetValue(clampedScaledValue, pm);
                }
            }
            OnShapeDimensionChanged(field, orig);
            MonoUtilities.RefreshPartContextWindow(part);
            return closeEnough;
        }

        public override void TranslateAttachmentsAndNodes(BaseField f, object obj)
        {
            // Len = MinorRadius * 2 == (outerDiameter - innerDiameter)
            if (f.name == nameof(outerDiameter) && obj is float oldOuterDiameter)
            {
                HandleDiameterChange((float)f.GetValue(this), oldOuterDiameter);
                float oldLen = oldOuterDiameter - innerDiameter;
                HandleLengthChange(Length, oldLen);
            } else if (f.name == nameof(innerDiameter) && obj is float oldInnerDiameter)
            {
                float oldLen = outerDiameter - oldInnerDiameter;
                HandleLengthChange(Length, oldLen);
            }
        }

        public override void UnNormalizeCylindricCoordinates(ShapeCoordinates coords)
        {
            throw new NotImplementedException();
        }

        public override void UpdateTechConstraints()
        {
            Fields[nameof(innerDiameter)].guiActiveEditor = PPart.diameterMin != PPart.diameterMax;
            UI_FloatEdit diameterEdit = Fields[nameof(innerDiameter)].uiControlEditor as UI_FloatEdit;
            diameterEdit.incrementLarge = PPart.diameterLargeStep;
            diameterEdit.incrementSmall = PPart.diameterSmallStep;

            Fields[nameof(outerDiameter)].guiActiveEditor = PPart.diameterMin != PPart.diameterMax;
            diameterEdit = Fields[nameof(outerDiameter)].uiControlEditor as UI_FloatEdit;
            diameterEdit.incrementLarge = PPart.diameterLargeStep;
            diameterEdit.incrementSmall = PPart.diameterSmallStep;

            AdjustDimensionBounds();
            innerDiameter = Mathf.Clamp(innerDiameter, diameterEdit.minValue, diameterEdit.maxValue);
            outerDiameter = Mathf.Clamp(outerDiameter, diameterEdit.minValue, diameterEdit.maxValue);
        }
        public override void UpdateTFInterops()
        {
            ProceduralPart.tfInterface.InvokeMember("AddInteropValue", ProceduralPart.tfBindingFlags, null, null, new System.Object[] { this.part, "diam1", innerDiameter, "ProceduralParts" });
            ProceduralPart.tfInterface.InvokeMember("AddInteropValue", ProceduralPart.tfBindingFlags, null, null, new System.Object[] { this.part, "diam2", outerDiameter, "ProceduralParts" });
        }

        internal override void InitializeAttachmentNodes() => InitializeAttachmentNodes(Length, outerDiameter);

        internal override void UpdateShape(bool force = true)
        {
            part.CoMOffset = CoMOffset;
            Volume = CalculateVolume();
            GenerateMeshes(MajorRadius, MinorRadius, (int)numSides, 18);

            GenerateColliders();
            // WriteMeshes in AbstractSoRShape typically does UpdateNodeSize, UpdateProps, RaiseModelAndColliderChanged
            UpdateNodeSize(TopNodeName);
            UpdateNodeSize(BottomNodeName);
            PPart.UpdateProps();
            RaiseModelAndColliderChanged();
        }

        private void UpdateNodeSize(string nodeName)
        {
            if (part.attachNodes.Find(n => n.id == nodeName) is AttachNode node)
            {
                node.size = Math.Min((int)(innerDiameter / PPart.diameterLargeStep), 3);
                node.breakingTorque = node.breakingForce = Mathf.Max(50 * node.size * node.size, 50);
                RaiseChangeAttachNodeSize(node, innerDiameter, Mathf.PI * innerDiameter * innerDiameter * 0.25f);
            }
        }

        private void GenerateColliders()
        {
            foreach (var x in gameObject.GetComponentsInChildren<CapsuleCollider>())
                x.gameObject.DestroyGameObject();
            gameObject.GetComponentsInChildren<SphereCollider>().FirstOrDefault(c => c.name.Equals("Central_Sphere_Collider"))?.gameObject.DestroyGameObject();

            // The first corner is at angle=0.
            // We want to start the capsules in between the corners.
            float offset = (360f / numSides) / 2;
            Vector3 refPoint = new Vector3(MajorRadius, 0, 0);

            var colliderRadius = numSides switch
            {
                3 => MinorRadius / 2,
                4 => MinorRadius * 0.75f,
                5 => MinorRadius * 0.9f,
                _ => MinorRadius
            };
            colliderRadius *= 0.95f;
            for (int i=0; i<numSides; i++)
            {
                var go = new GameObject($"Capsule_Collider_{i}");
                var coll = go.AddComponent<CapsuleCollider>();
                go.transform.SetParent(PPart.gameObject.transform, false);
                coll.isTrigger = true;
                coll.center = Vector3.zero;
                coll.radius = colliderRadius;
                coll.direction = 0;
                var prevCornerOrient = Quaternion.AngleAxis(360f * i / numSides, Vector3.up);
                var prevCornerPos = prevCornerOrient * refPoint;
                var nextCornerOrient = Quaternion.AngleAxis(360f * (i+1) / numSides, Vector3.up);
                var nextCornerPos = nextCornerOrient * refPoint;
                coll.height = (nextCornerPos - prevCornerPos).magnitude + colliderRadius * 2;
                var orientation = Quaternion.AngleAxis(90 + offset + (360f * i / numSides), Vector3.up);
                go.transform.localRotation *= orientation;
                go.transform.localPosition = (prevCornerPos + nextCornerPos) / 2;
            }
        }

        // This method taken from : http://wiki.unity3d.com/index.php/ProceduralPrimitives#C.23_-_Torus
        private void GenerateMeshes(float radius1, float radius2, int nbRadSeg, int nbSides)
        {
            //MeshFilter filter = gameObject.GetComponent<MeshFilter>();
            //Mesh mesh = filter.mesh;
            Mesh mesh = HighLogic.LoadedScene == GameScenes.LOADING ? PPart.SidesIconMesh : PPart.SidesMesh;
            mesh.Clear();
            PPart.EndsMesh.Clear();

            #region Vertices
            Vector3[] vertices = new Vector3[(nbRadSeg + 1) * (nbSides + 1)];
            float _2pi = Mathf.PI * 2f;
            for (int seg = 0; seg <= nbRadSeg; seg++)
            {
                int currSeg = seg == nbRadSeg ? 0 : seg;

                float t1 = (float)currSeg / nbRadSeg * _2pi;
                Vector3 r1 = new Vector3(Mathf.Cos(t1) * radius1, 0f, Mathf.Sin(t1) * radius1);

                for (int side = 0; side <= nbSides; side++)
                {
                    int currSide = side == nbSides ? 0 : side;

                    Vector3 normale = Vector3.Cross(r1, Vector3.up);
                    float t2 = (float)currSide / nbSides * _2pi;
                    Vector3 r2 = Quaternion.AngleAxis(-t1 * Mathf.Rad2Deg, Vector3.up) * new Vector3(Mathf.Sin(t2) * radius2, Mathf.Cos(t2) * radius2);

                    vertices[side + seg * (nbSides + 1)] = r1 + r2;
                }
            }
            #endregion

            #region Normales
            Vector3[] normales = new Vector3[vertices.Length];
            for (int seg = 0; seg <= nbRadSeg; seg++)
            {
                int currSeg = seg == nbRadSeg ? 0 : seg;

                float t1 = (float)currSeg / nbRadSeg * _2pi;
                Vector3 r1 = new Vector3(Mathf.Cos(t1) * radius1, 0f, Mathf.Sin(t1) * radius1);

                for (int side = 0; side <= nbSides; side++)
                {
                    normales[side + seg * (nbSides + 1)] = (vertices[side + seg * (nbSides + 1)] - r1).normalized;
                }
            }
            #endregion

            #region UVs
            Vector2[] uvs = new Vector2[vertices.Length];
            for (int seg = 0; seg <= nbRadSeg; seg++)
                for (int side = 0; side <= nbSides; side++)
                    uvs[side + seg * (nbSides + 1)] = new Vector2((float)seg / nbRadSeg, (float)side / nbSides);
            #endregion

            #region Triangles
            int nbFaces = vertices.Length;
            int nbTriangles = nbFaces * 2;
            int nbIndexes = nbTriangles * 3;
            int[] triangles = new int[nbIndexes];

            int i = 0;
            for (int seg = 0; seg <= nbRadSeg; seg++)
            {
                for (int side = 0; side <= nbSides - 1; side++)
                {
                    int current = side + seg * (nbSides + 1);
                    int next = side + (seg < (nbRadSeg) ? (seg + 1) * (nbSides + 1) : 0);

                    if (i < triangles.Length - 6)
                    {
                        triangles[i++] = current;
                        triangles[i++] = next;
                        triangles[i++] = next + 1;

                        triangles[i++] = current;
                        triangles[i++] = next + 1;
                        triangles[i++] = current + 1;
                    }
                }
            }
            #endregion

            mesh.vertices = vertices;
            mesh.normals = normales;
            mesh.uv = uvs;
            mesh.triangles = triangles;

            mesh.RecalculateBounds();
            mesh.Optimize();
        }
    }
}
