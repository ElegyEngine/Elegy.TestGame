// SPDX-FileCopyrightText: 2022-2023 Admer Šuko
// SPDX-License-Identifier: MIT

namespace TestGame
{
	public class Game : IApplication
	{
		public string Name => "Elegy test game";
		public string Error { get; private set; } = string.Empty;
		public bool Initialised { get; private set; } = false;

		public bool Init()
		{
			Console.Log( "[Game] Init" );
			Initialised = true;

			Elegy.Assets.ApplicationConfig gameConfig = FileSystem.CurrentConfig;

			Console.Log( $"[Game] Name: {gameConfig.Title}" );
			Console.Log( $"       Developer: {gameConfig.Developer}" );
			Console.Log( $"       Publisher: {gameConfig.Publisher}" );
			Console.Log( $"       Version: {gameConfig.Version}" );

			return true;
		}

		public bool Start()
		{
			Console.Log( "[Game] Start" );

			var setupControlAutoexpand = ( Control node, bool anchor ) =>
			{
				const int sizeFlags = (int)Control.SizeFlags.ExpandFill;
				node.SizeFlagsHorizontal = sizeFlags;
				node.SizeFlagsVertical = sizeFlags;
				if ( anchor )
				{
					node.LayoutMode = 1;
				}
				node.SetAnchorsPreset( Control.LayoutPreset.FullRect );
				return node;
			};

			var buttonTextAction = ( Control parent, string text, Action? onPressed ) =>
			{
				var button = parent.CreateChild<Button>();
				button.Text = text;
				if ( onPressed != null )
				{
					button.Pressed += onPressed;
				}
				return button;
			};

			mRootControl = setupControlAutoexpand( Nodes.CreateNode<Control>(), true );
			mRootControl.Size = mRootControl.GetViewportRect().Size;

			var panel = setupControlAutoexpand( mRootControl.CreateChild<Panel>(), true );
			panel.Size = mRootControl.Size;

			var hbox = setupControlAutoexpand( panel.CreateChild<HBoxContainer>(), true );
			hbox.Size = mRootControl.Size;

			var containerLeft = setupControlAutoexpand( hbox.CreateChild<VBoxContainer>(), true );
			// setupControlAutoexpand strictly returns Control, so we cast here for Alignment
			var container = setupControlAutoexpand( hbox.CreateChild<VBoxContainer>(), true ) as VBoxContainer;
			var containerRight = setupControlAutoexpand( hbox.CreateChild<VBoxContainer>(), true );

			container.CustomMinimumSize = new( 200.0f, 50.0f );
			container.Alignment = BoxContainer.AlignmentMode.Center;
			container.SizeFlagsStretchRatio = 0.25f;

			buttonTextAction( container, "Click me", () =>
			{
				StartGame( "maps/test" );
			} );
			
			buttonTextAction( container, "Exit", () =>
			{
				Console.Log( "[Game] Exiting..." );
				mUserWantsToExit = true;
			} );

			return true;
		}

		public void Shutdown()
		{
			Console.Log( "[Game] Shutdown" );
			mRootControl.QueueFree();
			mRootControl = null;

			mEntities.Clear();
		}

		public bool RunFrame( float delta )
		{
			// Quick little toggle quickly cobbled together,
			// until we have an extension to the input system
			if ( Input.IsKeyPressed( Key.Escape ) )
			{
				if ( !mEscapeWasHeld )
				{
					mRootControl.Visible = !mRootControl.Visible;
					Input.MouseMode = mRootControl.Visible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
				}
				mEscapeWasHeld = true;
			}
			else
			{
				mEscapeWasHeld = false;
			}

			mEntities?.ForEach( entity => entity.Think() );
			mClient?.Update();
			mClient?.UpdateController();

			return !mUserWantsToExit;
		}

		public void RunPhysicsFrame( float delta )
		{
			mEntities?.ForEach( entity => entity.PhysicsUpdate( delta ) );
		}

		public void HandleInput( InputEvent @event )
		{
			mClient?.UserInput( @event );
		}

		private void StartGame( string mapFile )
		{
			Console.Log( $"[Game] Starting '{mapFile}'" );

			mMap = Assets.MapDocument.FromValve220MapFile( $"{mapFile}.map" );
			if ( mMap == null )
			{
				Console.Error( $"[Game.StartGame] Failed to load '{mapFile}'" );
				return;
			}

			mEntities = new();
			mClient = new()
			{
				Controller = CreateEntity<Entities.Player>()
			};

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
				default: Console.Log( $"[Game.SpawnEntity]: unknown map entity class '{mapEntity.ClassName}'", ConsoleMessageType.Developer ); return;
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
		}

		private T CreateEntity<T>() where T : Entities.Entity, new()
		{
			T entity = new();
			entity.Spawn();
			mEntities.Add( entity );

			return entity;
		}

		private Client.Client? mClient;
		private List<Entities.Entity> mEntities = new();
		private Assets.MapDocument? mMap;
		private Node3D mWorldspawnNode;

		private bool mEscapeWasHeld = false;
		private Control mRootControl;
		private bool mUserWantsToExit = false;
	}
}
