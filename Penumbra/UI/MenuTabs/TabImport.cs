using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dalamud.Plugin;
using ImGuiNET;
using Penumbra.Importer;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabImport
        {
            private const string LabelTab               = "Import Mods";
            private const string LabelImportButton      = "Import TexTools Modpacks";
            private const string LabelFileDialog        = "Pick one or more modpacks.";
            private const string LabelFileImportRunning = "Import in progress...";
            private const string FileTypeFilter         = "TexTools TTMP Modpack (*.ttmp2)|*.ttmp*|All files (*.*)|*.*";
            private const string TooltipModpack1        = "Writing modpack to disk before extracting...";
            private const string FailedImport           = "One or more of your modpacks failed to import.\nPlease submit a bug report.";

            private const uint ColorRed    = 0xFF0000C8;
            private const uint ColorYellow = 0xFF00C8C8;

            private static readonly Vector2 ImportBarSize = new( -1, 0 );

            private          bool              _isImportRunning;
            private          bool              _hasError;
            private          TexToolsImport?   _texToolsImport;
            private readonly SettingsInterface _base;
            private readonly ModManager        _manager;

            public TabImport( SettingsInterface ui )
            {
                _base    = ui;
                _manager = Service< ModManager >.Get();
            }

            public bool IsImporting()
                => _isImportRunning;

            private void RunImportTask()
            {
                _isImportRunning = true;
                Task.Run( async () =>
                {
                    try
                    {
                        var picker = new OpenFileDialog
                        {
                            Multiselect     = true,
                            Filter          = FileTypeFilter,
                            CheckFileExists = true,
                            Title           = LabelFileDialog,
                        };

                        var result = await picker.ShowDialogAsync();

                        if( result == DialogResult.OK )
                        {
                            _hasError = false;

                            foreach( var fileName in picker.FileNames )
                            {
                                PluginLog.Log( $"-> {fileName} START" );

                                try
                                {
                                    _texToolsImport = new TexToolsImport( _manager.BasePath );
                                    _texToolsImport.ImportModPack( new FileInfo( fileName ) );

                                    PluginLog.Log( $"-> {fileName} OK!" );
                                }
                                catch( Exception ex )
                                {
                                    PluginLog.LogError( ex, "Failed to import modpack at {0}", fileName );
                                    _hasError = true;
                                }
                            }

                            var directory = _texToolsImport?.ExtractedDirectory;
                            _texToolsImport = null;
                            _base.ReloadMods();
                            if( directory != null )
                            {
                                _base._menu.InstalledTab.Selector.SelectModByDir( directory.Name );
                            }
                        }
                    }
                    catch( Exception e )
                    {
                        PluginLog.Error( $"Error opening file picker dialogue:\n{e}" );
                    }

                    _isImportRunning = false;
                } );
            }

            private void DrawImportButton()
            {
                if( !_manager.Valid )
                {
                    ImGui.PushStyleVar( ImGuiStyleVar.Alpha, 0.5f );
                    ImGui.Button( LabelImportButton );
                    ImGui.PopStyleVar();

                    ImGui.PushStyleColor( ImGuiCol.Text, ColorRed );
                    ImGui.Text( "Can not import since the mod directory path is not valid." );
                    ImGui.Dummy( Vector2.UnitY * ImGui.GetTextLineHeightWithSpacing() );
                    ImGui.PopStyleColor();

                    ImGui.Text( "Please set the mod directory in the settings tab." );
                    ImGui.Text( "This folder should preferably be close to the root directory of your (preferably SSD) drive, for example" );
                    ImGui.PushStyleColor( ImGuiCol.Text, ColorYellow );
                    ImGui.Text( "        D:\\ffxivmods" );
                    ImGui.PopStyleColor();
                    ImGui.Text( "You can return to this tab once you've done that." );
                }
                else if( ImGui.Button( LabelImportButton ) )
                {
                    RunImportTask();
                }
            }

            private void DrawImportProgress()
            {
                ImGui.Button( LabelFileImportRunning );

                if( _texToolsImport == null )
                {
                    return;
                }

                switch( _texToolsImport.State )
                {
                    case ImporterState.None: break;
                    case ImporterState.WritingPackToDisk:
                        ImGui.Text( TooltipModpack1 );
                        break;
                    case ImporterState.ExtractingModFiles:
                    {
                        var str =
                            $"{_texToolsImport.CurrentModPack} - {_texToolsImport.CurrentProgress} of {_texToolsImport.TotalProgress} files";

                        ImGui.ProgressBar( _texToolsImport.Progress, ImportBarSize, str );
                        break;
                    }
                    case ImporterState.Done: break;
                    default:                 throw new ArgumentOutOfRangeException();
                }
            }

            private static void DrawFailedImportMessage()
            {
                ImGui.PushStyleColor( ImGuiCol.Text, ColorRed );
                ImGui.Text( FailedImport );
                ImGui.PopStyleColor();
            }

            public void Draw()
            {
                var ret = ImGui.BeginTabItem( LabelTab );
                if( !ret )
                {
                    return;
                }

                if( !_isImportRunning )
                {
                    DrawImportButton();
                }
                else
                {
                    DrawImportProgress();
                }

                if( _hasError )
                {
                    DrawFailedImportMessage();
                }

                ImGui.EndTabItem();
            }
        }
    }
}