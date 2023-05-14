
namespace Game.Assets
{
	public static class MaterialManager
	{
		private const string Tag = "MaterialManager";

		private static Dictionary<string, Material> mMaterialDictionary = new();

		public readonly static Material MissingMaterial = new StandardMaterial3D()
		{
			AlbedoColor = Color.Color8( 255, 128, 192 ),
			Roughness = 0.5f,
			Metallic = 0.5f
		};

		public static Material GetMaterial( string path )
		{
			if ( mMaterialDictionary.ContainsKey( path ) )
			{
				return mMaterialDictionary[path];
			}

			// TODO: right now we're hardcoded to load PNGs
			string texturePath = path + ".png";
			string? realPath = FileSystem.PathTo( texturePath, PathFlags.File );
			if ( realPath is null )
			{
				Console.Warning( Tag, $"Can't find material '{path}', using default material..."  );

				// Performance optimisation
				mMaterialDictionary[path] = MissingMaterial;
				return MissingMaterial;
			}

			Console.Log( Tag, $"Loaded {Console.Green}'{path}'" );

			return new StandardMaterial3D()
			{
				ResourceName = path,

				Roughness = 1.0f,
				Metallic = 0.0f,
				MetallicSpecular = 0.0f,
				SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
				TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmapsAnisotropic,

				AlbedoTexture = ImageTexture.CreateFromImage( Image.LoadFromFile( realPath ) )
			};
		}
	}
}
