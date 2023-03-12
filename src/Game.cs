// SPDX-FileCopyrightText: 2022-2023 Admer Šuko
// SPDX-License-Identifier: MIT

namespace TestGame
{
	public class Game : IApplication
	{
		public string Name => "Elegy test game";
		public string Error { get; private set; } = string.Empty;
		public bool Initialised { get; private set; } = false;

		public const string Tag = "Game";

		public bool Init()
		{
			Console.Log( Tag, "Init" );
			Initialised = true;

			Elegy.Assets.ApplicationConfig gameConfig = FileSystem.CurrentConfig;

			Console.Log( Tag,   $"Name:      {gameConfig.Title}" );
			Console.Log( $"       Developer: {gameConfig.Developer}" );
			Console.Log( $"       Publisher: {gameConfig.Publisher}" );
			Console.Log( $"       Version:   {gameConfig.Version}" );

			return true;
		}

		public bool Start()
		{
			Console.Log( Tag, "Start" );

			mMenu.OnNewGame = ( string mapName ) =>
			{
				StartGame( mapName );
				mMenu.Visible = false;
			};

			mMenu.OnLeaveGame = () =>
			{
				LeaveGame();
			};

			mMenu.OnExit = () =>
			{
				LeaveGame();
				mUserWantsToExit = true;
			};

			mMenu.Init();

			return true;
		}

		public void Shutdown()
		{
			Console.Log( Tag, "Shutdown" );
			
			mEntities.Clear();
			mClient = null;
		}

		private void ToggleMenu()
		{
			if ( !mGameIsLoaded )
			{
				mMenu.Visible = true;
				Input.MouseMode = Input.MouseModeEnum.Visible;
				return;
			}

			mMenu.Visible = !mMenu.Visible;
			Input.MouseMode = mMenu.Visible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
		}

		public bool RunFrame( float delta )
		{
			// Quick little toggle quickly cobbled together,
			// until we have an extension to the input system
			if ( Input.IsKeyPressed( Key.Escape ) )
			{
				if ( !mEscapeWasHeld )
				{
					ToggleMenu();
				}
				mEscapeWasHeld = true;
			}
			else
			{
				mEscapeWasHeld = false;
			}

			if ( mGameIsLoaded )
			{
				mEntities.ForEach( entity => entity.Think() );
				mClient.Update();
				mClient.UpdateController();
			}

			return !mUserWantsToExit;
		}

		public void RunPhysicsFrame( float delta )
		{
			if ( mGameIsLoaded )
			{
				mEntities.ForEach( entity => entity.PhysicsUpdate( delta ) );
			}
		}

		public void HandleInput( InputEvent @event )
		{
			if ( mGameIsLoaded )
			{
				mClient.UserInput( @event );
			}
		}

		private void StartGame( string mapFile )
		{

			Console.Log( Tag, $"Starting 'maps/{mapFile}'" );

			mMap = Assets.MapDocument.FromValve220MapFile( $"maps/{mapFile}" );
			if ( mMap is null )
			{
				Console.Error( "Game.StartGame", $"Failed to load 'maps/{mapFile}'" );
				return;
			}

			mEntities = new();
			
			mWorldspawnNode = Assets.MapGeometry.CreateBrushModelNode( mMap.MapEntities[0] );
			mMap.MapEntities.ForEach( mapEntity =>
			{
				Entities.Entity? entity = null;

				// TODO: MapEntity attribute that glues the classname to the class
				switch ( mapEntity.ClassName )
				{
				case "light": entity = CreateEntity<Entities.Light>(); break;
				case "func_detail": entity = CreateEntity<Entities.FuncDetail>(); break;
				case "func_breakable": entity = CreateEntity<Entities.FuncBreakable>(); break;
				case "func_rotating": entity = CreateEntity<Entities.FuncRotating>(); break;
				case "func_water": entity = CreateEntity<Entities.FuncWater>(); break;
				case "prop_test": entity = CreateEntity<Entities.PropTest>(); break;
				default: Console.Log( "Game.StartGame", $"{Console.Yellow}Unknown map entity class {Console.White}'{mapEntity.ClassName}'", ConsoleMessageType.Developer ); return;
				}

				// This is a brush entity
				if ( mapEntity.Brushes.Count > 0 )
				{
					entity.AddBrushModel( mapEntity );
				}

				// Actually KeyValue should be called BEFORE Spawn, but oh well
				entity.KeyValue( mapEntity.Pairs );
			} );

			mEntities.ForEach( entity => entity.PostSpawn() );

			mClient = new()
			{
				Controller = CreateEntity<Entities.Player>()
			};

			Console.Success( Tag, "Map successfully loaded, enjoy" );
			mGameIsLoaded = true;
		}

		void LeaveGame()
		{
			if ( !mGameIsLoaded )
			{
				return;
			}

			Console.Log( Tag, "Leaving the game..." );

			mMap = null;
			mClient = null;
			mEntities.ForEach( entity => entity.Destroy() );
			mEntities.Clear();

			mWorldspawnNode.QueueFree();

			mGameIsLoaded = false;
		}

		private T CreateEntity<T>() where T : Entities.Entity, new()
		{
			T entity = new();
			entity.Spawn();
			mEntities.Add( entity );

			return entity;
		}

		private Client.Client? mClient;
		private Client.MainMenu mMenu = new();
		private List<Entities.Entity> mEntities = new();
		private Assets.MapDocument? mMap;
		private Node3D mWorldspawnNode;

		private bool mGameIsLoaded = false;
		private bool mEscapeWasHeld = false;
		private bool mUserWantsToExit = false;
	}
}
