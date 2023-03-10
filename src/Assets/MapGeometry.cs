// SPDX-FileCopyrightText: 2022-2023 Admer Šuko
// SPDX-License-Identifier: MIT

using Elegy.Geometry;

namespace TestGame.Assets
{
	public class MapGeometry
	{
		private static void IntersectPolygonWithOthers( ref Polygon3D polygon, List<MapFace> faces, int skipIndex )
		{
			for ( int i = 0; i < faces.Count; i++ )
			{
				Plane intersector = faces[i].Plane;
				if ( i == skipIndex )
				{
					continue;
				}

				var splitResult = polygon.Split( intersector );
				if ( splitResult.DidIntersect )
				{
					// Modify the polygon we started off from
					polygon = splitResult.Back ?? polygon;
				}
			}
		}

		private static void CreateBrushPolygons( ref List<MapFace> faces, float radius = 4096.0f, float scale = 1.0f / 39.37f )
		{
			for ( int i = 0; i < faces.Count; i++ )
			{
				Plane plane = faces[i].Plane;

				// Create a polygon in the centre of the world
				Polygon3D poly = new Polygon3D( plane, radius );

				// Then align its centre to the centre of this face... if we got any
				// Otherwise precision issues will occur
				Vector3 shift = faces[i].Centre - poly.Origin;
				poly.Shift( shift );

				// Intersect current face with all other faces
				IntersectPolygonWithOthers( ref poly, faces, i );

				// Axis:    Quake: Godot:
				// Forward  +X     -Z
				// Right    -Y     +X
				// Up       +Z     +Y
				for ( int p = 0; p < poly.Points.Count; p++ )
				{
					poly.Points[p] = poly.Points[p]
						// Snap to a grid of 0.25 to avoid some micro gaps
						.Snapped( Vector3.One * 0.25f )
						.ToGodot( scale );
				}

				// Finally add the subdivided polygon
				faces[i].Polygon = poly;
			}
		}

		private class MapRenderSurface
		{
			public List<Vector3> Vertices = new();
			public List<Vector3> Normals = new();
			public List<Vector2> Uvs = new();
			public List<int> VertexIndices = new();
			public int VertexCount = 0;
		}

		private static void AppendMapSurfaceToMesh( ArrayMesh mesh, MapMaterial material, MapRenderSurface surface )
		{
			SurfaceTool builder = new();
			builder.Begin( Mesh.PrimitiveType.Triangles );

			for ( int vertexId = 0; vertexId < surface.Vertices.Count; vertexId++ )
			{
				builder.SetUV( surface.Uvs[vertexId] );
				builder.SetNormal( surface.Normals[vertexId] );
				builder.AddVertex( surface.Vertices[vertexId] );
			}

			surface.VertexIndices.ForEach( index => builder.AddIndex( index ) );
			builder.GenerateTangents();
			builder.SetMaterial( material.EngineMaterial );
			builder.Commit( mesh );
		}

		public static Node3D CreateBrushModelNode( MapEntity brushEntity )
		{
			Vector3 brushOrigin = GetBrushOrigin( brushEntity ).ToGodot();
			brushEntity.Pairs["origin"] = $"{brushOrigin.X} {brushOrigin.Y} {brushOrigin.Z}";
			
			brushEntity.Brushes.ForEach( brush => CreateBrushPolygons( ref brush.Faces ) );

			Dictionary<MapMaterial, MapRenderSurface> renderSurfaces = new();
			
			brushEntity.Brushes.ForEach( brush =>
			{
				brush.Faces.ForEach( face =>
				{
					if ( face.MaterialName == "NULL" || face.MaterialName == "ORIGIN" )
					{
						return;
					}

					// Subdivide the polygon into triangles
					Polygon3D polygon = face.Polygon;
					if ( !polygon.IsValid() )
					{
						return;
					}

					MapRenderSurface renderSurface = renderSurfaces.GetOrAdd( face.Material, new() );
					for ( int i = 2; i < polygon.Points.Count; i++ )
					{
						renderSurface.VertexIndices.Add( renderSurface.VertexCount );
						renderSurface.VertexIndices.Add( renderSurface.VertexCount + i - 1 );
						renderSurface.VertexIndices.Add( renderSurface.VertexCount + i );
					}
					renderSurface.VertexCount += polygon.Points.Count;

					Vector3 planeNormal = polygon.Plane.Normal;
					polygon.Points.ForEach( position =>
					{
						renderSurface.Uvs.Add( face.CalculateUV( position, face.Material.Width, face.Material.Height ) );
						renderSurface.Normals.Add( planeNormal );
						renderSurface.Vertices.Add( position - brushOrigin );
					} );
				} );
			} );

			ArrayMesh mesh = new ArrayMesh();
			for ( int renderSurfaceId = 0; renderSurfaceId < renderSurfaces.Count; renderSurfaceId++ )
			{
				MapMaterial mapMaterial = renderSurfaces.Keys.ElementAt( renderSurfaceId );
				MapRenderSurface surface = renderSurfaces.Values.ElementAt( renderSurfaceId );

				AppendMapSurfaceToMesh( mesh, mapMaterial, surface );
			}

			Node3D parentNode = Nodes.CreateNode<Node3D>();
			parentNode.Name = brushEntity.ClassName;

			MeshInstance3D meshInstance = parentNode.CreateChild<MeshInstance3D>();
			meshInstance.Mesh = mesh;
			meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.DoubleSided;

			StaticBody3D staticBody = parentNode.CreateChild<StaticBody3D>();
			CollisionShape3D collisionShape = staticBody.CreateChild<CollisionShape3D>();
			collisionShape.Shape = Nodes.CreateCollisionShape( mesh );

			return parentNode;
		}

		private static Vector3 GetBrushOrigin( MapEntity brushEntity )
		{
			if ( brushEntity.ClassName == "worldspawn" )
			{
				return Vector3.Zero;
			}

			Vector3 origin = new();
			int count = 0;

			// The brush polygons haven't been created yet, but we
			// likely have enough information from the plane definitions
			brushEntity.Brushes.ForEach( brush =>
			{
				brush.Faces.ForEach( face =>
				{
					if ( face.MaterialName != "ORIGIN" )
					{
						return;
					}

					origin += face.Centre;
					count++;
				} );
			} );

			if ( count == 0 )
			{
				return brushEntity.BoundingBox.GetCenter();
			}

			return origin / count;
		}
	}
}
