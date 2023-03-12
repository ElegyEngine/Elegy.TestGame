// SPDX-FileCopyrightText: 2022-2023 Admer Šuko
// SPDX-License-Identifier: MIT

using Elegy.Geometry;
using Elegy.Text;

namespace TestGame.Assets
{
	public class MapMaterial
	{
		public string Name = string.Empty;
		public Material EngineMaterial;
		public ImageTexture DiffuseTexture;

		public const string Tag = "MapMaterial";

		public int Width => DiffuseTexture?.GetWidth() ?? 128;
		public int Height => DiffuseTexture?.GetHeight() ?? 128;

		public static Dictionary<string, MapMaterial> Materials { get; private set; } = new();

		public readonly static MapMaterial Default = new()
		{
			Name = "Default",
			EngineMaterial = new StandardMaterial3D()
			{
				AlbedoColor = new Color( 1.0f, 0.5f, 0.7f ),
				Roughness = 0.5f,
				Metallic = 0.5f
			}
		};

		public static MapMaterial Load( string materialName )
		{
			if ( materialName == "NULL" || materialName == "ORIGIN" )
			{
				return Default;
			}

			if ( Materials.TryGetValue( materialName, out MapMaterial? existingMaterial ) )
			{
				return existingMaterial;
			}

			string path = $"textures/{materialName}.png";
			if ( !FileSystem.Exists( path ) )
			{
				Console.Error( Tag, $"Cannot find image '{path}', oops" );
				Materials.Add( materialName, Default ); // Performance optimisation
				return Default;
			}

			Image image = Image.LoadFromFile( FileSystem.PathTo( path, PathFlags.File ) );
			ImageTexture texture = ImageTexture.CreateFromImage( image );

			StandardMaterial3D material = new();
			material.ResourceName = $"{materialName}";
			material.AlbedoTexture = texture;
			material.Roughness = 1.0f;
			material.Metallic = 0.0f;
			material.MetallicSpecular = 0.0f;
			material.SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled;
			material.TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmapsAnisotropic;

			if ( materialName.Contains( '~' ) )
			{
				material.EmissionEnabled = true;
				//material.EmissionTexture = texture;
				material.Emission = new Color( 1.0f, 1.0f, 1.0f, 1.0f );
				material.EmissionEnergyMultiplier = 2.5f;
			}
			else if ( materialName[0] == '{' )
			{
				material.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
				material.AlphaScissorThreshold = 0.5f;
				//material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
			}
			else if ( materialName[0] == '!' || materialName.StartsWith( "WATER" ) || materialName.Contains( "GLASS" ) )
			{
				material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
				material.AlbedoColor = new Color( 1.0f, 1.0f, 1.0f, 0.5f );
			}

			MapMaterial mapMaterial = new()
			{
				Name = materialName,
				EngineMaterial = material,
				DiffuseTexture = texture
			};
			Materials.Add( materialName, mapMaterial );
			return mapMaterial;
		}
	}

	public class MapFace
	{
		public Vector3[] PlaneDefinition = new Vector3[3];
		public Plane Plane = new();

		public string MaterialName = string.Empty;
		// XYZ -> axis; W -> offset along axis
		public Vector4[] ProjectionUVS = new Vector4[2];
		public float Rotation = 0.0f;
		public Vector2 Scale = Vector2.One;

		public Vector3 Centre => (PlaneDefinition[0] + PlaneDefinition[1] + PlaneDefinition[2]) / 3.0f;

		public Vector2 CalculateUV( Vector3 point, int imageWidth, int imageHeight, float scale = 39.37f )
		{
			Vector3 axisU = ProjectionUVS[0].ToVector3().ToGodot() * (1.0f / Scale.X) * scale * scale;
			Vector3 axisV = ProjectionUVS[1].ToVector3().ToGodot() * (1.0f / Scale.Y) * scale * scale;

			return new()
			{
				X = (point.Dot( axisU ) + ProjectionUVS[0].W) / imageWidth,
				Y = (point.Dot( axisV ) + ProjectionUVS[1].W) / imageHeight
			};
		}

		// Filled in later
		public Polygon3D Polygon = new();
		public MapMaterial Material;
	}

	public class MapBrush
	{
		public Vector3 Centre = Vector3.Zero;
		public Aabb BoundingBox = new();
		public List<MapFace> Faces = new();
	}

	public class MapEntity
	{
		public Vector3 Centre = Vector3.Zero;
		public Aabb BoundingBox = new();
		public List<MapBrush> Brushes = new();

		public string ClassName = string.Empty;
		public Dictionary<string, string> Pairs = new();
	}

	public class MapDocument
	{
		public const string Tag = "MapDocument";

		// ( x1 y1 z1 ) ( x2 y2 z2 ) ( x3 y3 z3 ) texture_name [ ux uy uz offsetX ] [ vx vy vz offsetY ] rotation scaleX scaleY
		private static MapFace ParseFace( Lexer lex )
		{
			MapFace face = new();

			for ( int i = 0; i < 3; i++ )
			{
				// Eat the (
				if ( !lex.Expect( "(", true ) )
				{
					throw new Exception( $"Expected '(' {lex.GetLineInfo()}" );
				}

				face.PlaneDefinition[i].X = Parse.Float( lex.Next() );
				face.PlaneDefinition[i].Y = Parse.Float( lex.Next() );
				face.PlaneDefinition[i].Z = Parse.Float( lex.Next() );
				
				// Eat the )
				if ( !lex.Expect( ")", true ) )
				{
					throw new Exception( $"Expected ')' {lex.GetLineInfo()}" );
				}
			}

			face.Plane = new Plane( face.PlaneDefinition[0], face.PlaneDefinition[1], face.PlaneDefinition[2] );

			// We could potentially have slashes in here and all kinds of wacky characters
			lex.IgnoreDelimiters = true;
			face.MaterialName = lex.Next();
			lex.IgnoreDelimiters = false;

			if ( face.MaterialName == string.Empty )
			{
				throw new Exception( $"Texture or material is empty {lex.GetLineInfo()}" );
			}

			for ( int i = 0; i < 2; i++ )
			{
				if ( !lex.Expect( "[", true ) )
				{
					throw new Exception( $"Expected '[' {lex.GetLineInfo()}" );
				}

				string token = lex.Next();
				face.ProjectionUVS[i].X = Parse.Float( token );
				token = lex.Next();
				face.ProjectionUVS[i].Y = Parse.Float( token );
				token = lex.Next();
				face.ProjectionUVS[i].Z = Parse.Float( token );
				token = lex.Next();
				face.ProjectionUVS[i].W = Parse.Float( token );

				if ( !lex.Expect( "]", true ) )
				{
					throw new Exception( $"Expected ']' {lex.GetLineInfo()}" );
				}
			}

			face.Rotation = Parse.Float( lex.Next() );
			face.Scale.X = Parse.Float( lex.Next() );
			face.Scale.Y = Parse.Float( lex.Next() );

			// This is an ugly, hacky way to support Quake 3's blessed map format
			string nextToken = lex.Peek();
			while ( nextToken != "}" && nextToken != "(" )
			{
				lex.Next();
				nextToken = lex.Peek();
				if ( nextToken == "}" || nextToken == "(" )
				{
					break;
				}
			}

			return face;
		}

		private static MapBrush ParseBrush( Lexer lex )
		{
			MapBrush brush = new();

			// Eat the {
			lex.Next();

			while ( true )
			{
				if ( lex.IsEnd() )
				{
					throw new Exception( $"Unexpected EOF {lex.GetLineInfo()}" );
				}

				// Eat the }
				if ( lex.Expect( "}", true ) )
				{
					break;
				}
				// It's a map face
				else if ( lex.Expect( "(" ) )
				{
					brush.Faces.Add( ParseFace( lex ) );
				}
				// Forgor to add this
				else
				{
					throw new Exception( $"Unexpected token '{lex.Next()}' {lex.GetLineInfo()}" );
				}
			}

			brush.BoundingBox = new Aabb( brush.Faces[0].Centre, Vector3.One * 0.001f );
			brush.Faces.ForEach( face =>
			{
				for ( int i = 0; i < 3; i++  )
				{
					brush.BoundingBox = brush.BoundingBox.Expand( face.PlaneDefinition[i] );
				}
			} );

			return brush;
		}

		private static MapEntity ParseEntity( Lexer lex )
		{
			MapEntity entity = new();

			while ( true )
			{
				if ( lex.IsEnd() )
				{
					throw new Exception( $"Unexpected EOF {lex.GetLineInfo()}" );
				}

				// Closure of this entity
				if ( lex.Expect( "}", true ) )
				{
					break;
				}
				// New brush
				else if ( lex.Expect( "{" ) )
				{
					entity.Brushes.Add( ParseBrush( lex ) );
				}
				// Key-value pair
				else
				{
					string key = lex.Next();

					lex.IgnoreDelimiters = true;
					string value = lex.Next();
					lex.IgnoreDelimiters = false;

					entity.Pairs.Add( key, value );
				}
			}

			if ( entity.Pairs.TryGetValue( "classname", out string? className ) )
			{
				entity.ClassName = className;
			}
			else
			{
				Console.Warning( Tag, $"Entity does not have a classname! {lex.GetLineInfo()}" );
				entity.ClassName = "__empty";
			}

			if ( entity.Pairs.TryGetValue( "origin", out string? originString ) )
			{
				entity.Centre = originString.ToVector3().ToGodot();
				entity.Pairs["origin"] = $"{entity.Centre.X} {entity.Centre.Y} {entity.Centre.Z}";
			}

			if ( entity.Brushes.Count > 0 )
			{
				entity.BoundingBox = entity.Brushes[0].BoundingBox;
				for ( int i = 1; i < entity.Brushes.Count; i++ )
				{
					entity.BoundingBox = entity.BoundingBox.Merge( entity.Brushes[i].BoundingBox );
				}
			}

			return entity;
		}

		public static MapDocument? FromValve220MapFile( string path )
		{
			if ( !FileSystem.Exists( path, PathFlags.File ) )
			{
				Console.Error( Tag, $"Cannot find file '{path}', oops" );
				return null;
			}

			string? actualPath = FileSystem.PathTo( path, PathFlags.File );
			if ( actualPath == null )
			{
				return null;
			}

			File.ReadAllText( actualPath );

			Console.Log( Tag, $"Loading map '{actualPath}'..." );

			MapDocument map = new();
			try
			{
				Lexer lex = new( File.ReadAllText( actualPath ) );
				while ( !lex.IsEnd() )
				{
					string token = lex.Next();

					if ( token == "{" )
					{
						map.MapEntities.Add( ParseEntity( lex ) );
					}
					else if ( token == "}" || token == string.Empty )
					{
						break;
					}
					else
					{
						throw new Exception( $"Unknown token '{token}' {lex.GetLineInfo()}" );
					}
				}
			}
			catch ( Exception exception )
			{
				Console.Error( Tag, $"Error while parsing .map: {exception.Message}" );
				Console.Log( Tag, $"Stack trace: {exception.StackTrace}", ConsoleMessageType.Developer );
				return map;
			}

			try
			{
				map.MapEntities.ForEach( entity =>
				{
					entity.Brushes.ForEach( brush =>
					{
						brush.Faces.ForEach( face =>
						{
							face.Material = MapMaterial.Load( face.MaterialName );
						} );
					} );
				} );
			}
			catch ( Exception ex )
			{
				Console.Error( Tag, $"Exception: {ex.Message}\n{ex.StackTrace}" );
			}

			return map;
		}

		public string Title = "unknown";
		public string Description = "unknown";
		public List<MapEntity> MapEntities = new();
	}
}
