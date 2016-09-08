﻿using EliteDangerousCompanionAppService;
using EliteDangerousDataDefinitions;
using EliteDangerousNetLogMonitor;
using EliteDangerousStarMapService;
using EliteDangerousSpeechService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Speech.Synthesis;
using EliteDangerousDataProviderService;
using Utilities;
using System.Threading;
using System.Globalization;
using System.Text.RegularExpressions;
using EDDI;

namespace configuration
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Profile profile;
        private ShipsConfiguration shipsConfiguration;

        private CompanionAppService companionAppService;

        private EDDIConfiguration eddiConfiguration;

        public MainWindow()
        {
            InitializeComponent();

            // Configure the EDDI tab
            eddiConfiguration = EDDIConfiguration.FromFile();
            eddiHomeSystemText.Text = eddiConfiguration.HomeSystem;
            eddiHomeStationText.Text = eddiConfiguration.HomeStation;
            eddiInsuranceDecimal.Value = eddiConfiguration.Insurance;
            eddiVerboseLogging.IsChecked = eddiConfiguration.Debug;
            //// DataGrid requires a list as a backing store
            //scripts = new List<Script>(eddiConfiguration.Scripts.Values);
            //scriptsData.ItemsSource = scripts;
            scriptsData.ItemsSource = eddiConfiguration.Scripts;

            Logging.Verbose = eddiConfiguration.Debug;

            // Configure the Companion App tab
            CompanionAppCredentials companionAppCredentials = CompanionAppCredentials.FromFile();
            companionAppEmailText.Text = companionAppCredentials.email;
            // See if the credentials work
            companionAppService = new CompanionAppService();
            try
            {
                profile = companionAppService.Profile();
                setUpCompanionAppComplete("Your connection to the companion app is operational, Commander " + profile.Cmdr.name);
            }
            catch (Exception)
            {
                if (companionAppService.CurrentState == CompanionAppService.State.NEEDS_LOGIN)
                {
                    // Fall back to stage 1
                    setUpCompanionAppStage1();
                }
                else if (companionAppService.CurrentState == CompanionAppService.State.NEEDS_CONFIRMATION)
                {
                    // Fall back to stage 2
                    setUpCompanionAppStage2();
                }
            }

            if (profile != null)
            {
                setShipyardFromConfiguration();
            }

            // Configure the NetLog tab
            NetLogConfiguration netLogConfiguration = NetLogConfiguration.FromFile();
            netLogPathTextBox.Text = netLogConfiguration.path;

            // Configure the EDSM tab
            StarMapConfiguration starMapConfiguration = StarMapConfiguration.FromFile();
            edsmApiKeyTextBox.Text = starMapConfiguration.apiKey;
            edsmCommanderNameTextBox.Text = starMapConfiguration.commanderName;

            // Configure the Text-to-speech tab
            SpeechServiceConfiguration speechServiceConfiguration = SpeechServiceConfiguration.FromFile();
            List<String> speechOptions = new List<String>();
            speechOptions.Add("Windows TTS default");
            try
            {
                using (SpeechSynthesizer synth = new SpeechSynthesizer())
                {
                    foreach (InstalledVoice voice in synth.GetInstalledVoices())
                    {
                        if (voice.Enabled)
                        {
                            speechOptions.Add(voice.VoiceInfo.Name);
                        }
                    }
                }

                ttsVoiceDropDown.ItemsSource = speechOptions;
                ttsVoiceDropDown.Text = speechServiceConfiguration.StandardVoice == null ? "Windows TTS default" : speechServiceConfiguration.StandardVoice;
            }
            catch (Exception e)
            {
                using (System.IO.StreamWriter errLog = new System.IO.StreamWriter(Environment.GetEnvironmentVariable("AppData") + @"\EDDI\speech.log", true))
                {
                   errLog.WriteLine("" + System.Threading.Thread.CurrentThread.ManagedThreadId + ": Caught exception " + e);
                }
            }
            ttsVolumeSlider.Value = speechServiceConfiguration.Volume;
            ttsRateSlider.Value = speechServiceConfiguration.Rate;
            ttsEffectsLevelSlider.Value = speechServiceConfiguration.EffectsLevel;
            ttsDistortCheckbox.IsChecked = speechServiceConfiguration.DistortOnDamage;

            ttsTestShipDropDown.ItemsSource = ShipDefinitions.ShipModels;
            ttsTestShipDropDown.Text = "Adder";
        }

        // Handle changes to the eddi tab
        private void homeSystemChanged(object sender, TextChangedEventArgs e)
        {
            updateEddiConfiguration();
        }

        private void homeStationChanged(object sender, TextChangedEventArgs e)
        {
            updateEddiConfiguration();
        }


        private void insuranceChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            updateEddiConfiguration();
        }

        private void verboseLoggingEnabled(object sender, RoutedEventArgs e)
        {
            updateEddiConfiguration();
        }

        private void verboseLoggingDisabled(object sender, RoutedEventArgs e)
        {
            updateEddiConfiguration();
        }

        private void updateEddiConfiguration()
        {
            EDDIConfiguration eddiConfiguration = new EDDIConfiguration();
            eddiConfiguration.HomeSystem = String.IsNullOrWhiteSpace(eddiHomeSystemText.Text) ? null : eddiHomeSystemText.Text.Trim();
            eddiConfiguration.HomeStation = String.IsNullOrWhiteSpace(eddiHomeStationText.Text) ? null : eddiHomeStationText.Text.Trim();
            eddiConfiguration.Insurance = eddiInsuranceDecimal.Value == null ? 5 : (decimal)eddiInsuranceDecimal.Value;
            eddiConfiguration.Debug = eddiVerboseLogging.IsChecked.Value;
            eddiConfiguration.Scripts = this.eddiConfiguration.Scripts;
            eddiConfiguration.ToFile();
        }

        // Handle changes to the companion app tab
        private void companionAppNextClicked(object sender, RoutedEventArgs e)
        {
            // See if the user is entering their email address and password
            if (companionAppEmailText.Visibility == Visibility.Visible)
            {
                // Stage 1 of authentication - login
                companionAppService.Credentials.email = companionAppEmailText.Text.Trim();
                companionAppService.Credentials.password = companionAppPasswordText.Password.Trim();
                try
                {
                    // It is possible that we have valid cookies at this point so don't log in, but we did
                    // need the credentials
                    if (companionAppService.CurrentState == CompanionAppService.State.NEEDS_LOGIN)
                    {
                        companionAppService.Login();
                    }
                    if (companionAppService.CurrentState == CompanionAppService.State.NEEDS_CONFIRMATION)
                    {
                        setUpCompanionAppStage2();
                    }
                    else if (companionAppService.CurrentState == CompanionAppService.State.READY)
                    {
                        if (profile == null)
                        {
                            profile = companionAppService.Profile();
                        }
                        setUpCompanionAppComplete("Your connection to the companion app is operational, Commander " + profile.Cmdr.name);
                        setShipyardFromConfiguration();
                    }
                }
                catch (EliteDangerousCompanionAppAuthenticationException ex)
                {
                    companionAppText.Text = ex.Message;
                }
                catch (EliteDangerousCompanionAppErrorException ex)
                {
                    companionAppText.Text = ex.Message;
                }
                catch (Exception ex)
                {
                    companionAppText.Text = "Unexpected problem\r\nPlease report this at http://github.com/CmdrMcDonald/EliteDangerousDataProvider/issues\r\n" + ex;
                }
            }
            else if (companionAppCodeText.Visibility == Visibility.Visible)
            {
                // Stage 2 of authentication - confirmation
                string code = companionAppCodeText.Text.Trim();
                try
                {
                    companionAppService.Confirm(code);
                    // All done - see if it works
                    profile = companionAppService.Profile();
                    setUpCompanionAppComplete("Your connection to the companion app is operational, Commander " + profile.Cmdr.name);
                    setShipyardFromConfiguration();
                }
                catch (EliteDangerousCompanionAppAuthenticationException ex)
                {
                    setUpCompanionAppStage1(ex.Message);
                }
                catch (EliteDangerousCompanionAppErrorException ex)
                {
                    setUpCompanionAppStage1(ex.Message);
                }
                catch (Exception ex)
                {
                    setUpCompanionAppStage1("Unexpected problem\r\nPlease report this at http://github.com/CmdrMcDonald/EliteDangerousDataProvider/issues\r\n" + ex);
                }
            }
        }

        private void setUpCompanionAppStage1(string message = null)
        {
            if (message == null)
            {
                companionAppText.Text = "You do not have a connection to the companion app at this time.  Please enter your Elite: Dangerous email address and password below";
            }
            else
            {
                companionAppText.Text = message;
            }

            companionAppEmailLabel.Visibility = Visibility.Visible;
            companionAppEmailText.Visibility = Visibility.Visible;
            companionAppEmailText.Text = companionAppService.Credentials.email;
            companionAppPasswordLabel.Visibility = Visibility.Visible;
            companionAppPasswordText.Visibility = Visibility.Visible;
            companionAppPasswordText.Password = companionAppService.Credentials.password;
            companionAppCodeText.Text = "";
            companionAppCodeLabel.Visibility = Visibility.Hidden;
            companionAppCodeText.Visibility = Visibility.Hidden;
            companionAppNextButton.Visibility = Visibility.Visible;
        }

        private void setUpCompanionAppStage2(string message = null)
        {
            if (message == null)
            {
                companionAppText.Text = "Please enter the verification code that should have been sent to your email address";
            }
            else
            {
                companionAppText.Text = message;
            }

            companionAppEmailLabel.Visibility = Visibility.Hidden;
            companionAppEmailText.Visibility = Visibility.Hidden;
            companionAppPasswordText.Password = "";
            companionAppPasswordLabel.Visibility = Visibility.Hidden;
            companionAppPasswordText.Visibility = Visibility.Hidden;
            companionAppCodeLabel.Visibility = Visibility.Visible;
            companionAppCodeText.Visibility = Visibility.Visible;
            companionAppNextButton.Visibility = Visibility.Visible;
        }

        private void setUpCompanionAppComplete(string message = null)
        {
            if (message == null)
            {
                companionAppText.Text = "Complete";
            }
            else
            {
                companionAppText.Text = message;
            }

            companionAppEmailLabel.Visibility = Visibility.Hidden;
            companionAppEmailText.Visibility = Visibility.Hidden;
            companionAppPasswordText.Password = "";
            companionAppPasswordLabel.Visibility = Visibility.Hidden;
            companionAppPasswordText.Visibility = Visibility.Hidden;
            companionAppCodeText.Text = "";
            companionAppCodeLabel.Visibility = Visibility.Hidden;
            companionAppCodeText.Visibility = Visibility.Hidden;
            companionAppNextButton.Visibility = Visibility.Hidden;
        }

        // Handle changes to NetLog tab
        private void netLogPathChanged(object sender, TextChangedEventArgs e)
        {
            updateNetLogConfiguration();
        }

        private void updateNetLogConfiguration()
        {
            NetLogConfiguration netLogConfiguration = new NetLogConfiguration();
            if (!String.IsNullOrWhiteSpace(netLogPathTextBox.Text))
            {
                netLogConfiguration.path = netLogPathTextBox.Text.Trim();
            }
            netLogConfiguration.ToFile();
        }

        private void netLogObtainClicked(object sender, RoutedEventArgs e)
        {
            List<string> processPaths = new Finder().GetPathFromProcess();
            if (processPaths.Count != 0)
            {
                netLogPathTextBox.Text = processPaths[0] + @"\Logs";
                updateNetLogConfiguration();
            }
            else
            {
                netLogText.Text = @"Unfortuantely we were unable to locate your product directory.  Please type in the location of the 'Logs' directory in your 'elite-dangerous-64' directory.  Possible locations include:";
                List<string> paths = new Finder().FindInstallationPaths();
                if (paths.Count == 0)
                {
                    paths.Add(Finder.DefProductsPath + @"\elite-dangerous-64");
                    paths.Add(Finder.DefLauncherPath + @"\elite-dangerous-64");
                    paths.Add(@"C:\Program Files (x86)\Steam\\SteamApps\common\Elite Dangerous\Products\elite-dangerous-64");
                }
                foreach (string path in paths)
                {
                    netLogText.Text += "\r\n\r\n" + path + @"\Logs";
                }
                netLogText.Text += "\r\n\r\nWhichever directory you select should contain a number of 'debugLog' files.";
            }
        }

        // Handle changes to EDSM tab
        private void edsmCommanderNameChanged(object sender, TextChangedEventArgs e)
        {
            updateEdsmConfiguration();
        }

        private void edsmApiKeyChanged(object sender, TextChangedEventArgs e)
        {
            updateEdsmConfiguration();
        }

        private void updateEdsmConfiguration()
        {
            StarMapConfiguration edsmConfiguration = new StarMapConfiguration();
            if (!String.IsNullOrWhiteSpace(edsmApiKeyTextBox.Text))
            {
                edsmConfiguration.apiKey = edsmApiKeyTextBox.Text.Trim();
            }
            if (!String.IsNullOrWhiteSpace(edsmCommanderNameTextBox.Text))
            {
                edsmConfiguration.commanderName = edsmCommanderNameTextBox.Text.Trim();
            }
            edsmConfiguration.ToFile();
        }

        // Handle changes to the Shipyard tab
        private void setShipyardFromConfiguration()
        {
            shipsConfiguration = new ShipsConfiguration();
            List<Ship> ships = new List<Ship>();
            if (profile != null)
            {
                ships.Add(profile.Ship);
                ships.AddRange(profile.StoredShips);
            }
            shipsConfiguration.Ships = ships;
            shipyardData.ItemsSource = ships;
        }

        private void testShipName(object sender, RoutedEventArgs e)
        {
            Ship ship = (Ship)((Button)e.Source).DataContext;
            ship.Health = 100;
            SpeechServiceConfiguration speechConfiguration = SpeechServiceConfiguration.FromFile();
            SpeechService speechService = new SpeechService(speechConfiguration);
            if (String.IsNullOrEmpty(ship.phoneticname))
            {
                speechService.Say(null, ship, ship.name + " stands ready.");
            }
            else
            {
                speechService.Say(null, ship, "<phoneme alphabet=\"ipa\" ph=\"" + ship.phoneticname + "\">" + ship.name + "</phoneme>" + " stands ready.");
            }
        }

        private void shipYardUpdated(object sender, DataTransferEventArgs e)
        {
            if (shipsConfiguration != null)
            {
                shipsConfiguration.ToFile();
            }            
        }

        // Handle Text-to-speech tab

        private void ttsVoiceDropDownUpdated(object sender, SelectionChangedEventArgs e)
        {
            ttsUpdated();
        }

        private void ttsEffectsLevelUpdated(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ttsUpdated();
        }

        private void ttsDistortionLevelUpdated(object sender, RoutedEventArgs e)
        {
            ttsUpdated();
        }

        private void ttsRateUpdated(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ttsUpdated();
        }

        private void ttsVolumeUpdated(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ttsUpdated();
        }

        private void ttsTestVoiceButtonClicked(object sender, RoutedEventArgs e)
        {
            Ship testShip = ShipDefinitions.ShipFromModel((string)ttsTestShipDropDown.SelectedValue);
            testShip.Health = 100;
            SpeechServiceConfiguration speechConfiguration = SpeechServiceConfiguration.FromFile();
            SpeechService speechService = new SpeechService(speechConfiguration);
            speechService.Say(null, testShip, "This is how I will sound in your " + Translations.ShipModel((string)ttsTestShipDropDown.SelectedValue) + ".");
        }

        private void ttsTestDamagedVoiceButtonClicked(object sender, RoutedEventArgs e)
        {
            Ship testShip = ShipDefinitions.ShipFromModel((string)ttsTestShipDropDown.SelectedValue);
            testShip.Health = 20;
            SpeechServiceConfiguration speechConfiguration = SpeechServiceConfiguration.FromFile();
            SpeechService speechService = new SpeechService(speechConfiguration);
            speechService.Say(null, testShip, "Severe damage to your " + Translations.ShipModel((string)ttsTestShipDropDown.SelectedValue) + ".");
        }

        /// <summary>
        /// fetch the Text-to-Speech Configuration and write it to File
        /// </summary>
        private void ttsUpdated()
        {
            SpeechServiceConfiguration speechConfiguration = new SpeechServiceConfiguration();
            speechConfiguration.StandardVoice = ttsVoiceDropDown.SelectedValue == null || ttsVoiceDropDown.SelectedValue.ToString() == "Windows TTS default" ? null : ttsVoiceDropDown.SelectedValue.ToString();
            speechConfiguration.Volume = (int)ttsVolumeSlider.Value;
            speechConfiguration.Rate = (int)ttsRateSlider.Value;
            speechConfiguration.EffectsLevel = (int)ttsEffectsLevelSlider.Value;
            speechConfiguration.DistortOnDamage = ttsDistortCheckbox.IsChecked.Value;
            speechConfiguration.ToFile();
        }

        /// <summary>
        /// Obtain the EDSM log and sync it with the local datastore
        /// </summary>
        private async void edsmObtainLogClicked(object sender, RoutedEventArgs e)
        {
            StarMapConfiguration starMapConfiguration = StarMapConfiguration.FromFile();

            string commanderName;
            if (String.IsNullOrEmpty(starMapConfiguration.commanderName))
            {
                // Fetch the commander name from the companion app
                CompanionAppService companionAppService = new CompanionAppService();
                Profile profile = companionAppService.Profile();
                if (profile != null && profile.Cmdr != null && profile.Cmdr.name != null)
                {
                    commanderName = profile.Cmdr.name;
                }
                else
                {
                    edsmFetchLogsButton.IsEnabled = false;
                    edsmFetchLogsButton.Content = "Companion app not configured and no name supplied; cannot obtain logs";
                    return;
                }
            }
            else
            {
                commanderName = starMapConfiguration.commanderName;
            }

            edsmFetchLogsButton.IsEnabled = false;
            edsmFetchLogsButton.Content = "Obtaining log...";

            var progress = new Progress<string>(s => edsmFetchLogsButton.Content = "Obtaining log..." + s);
            await Task.Factory.StartNew(() => obtainEdsmLogs(starMapConfiguration, commanderName, progress),
                                            TaskCreationOptions.LongRunning);
            edsmFetchLogsButton.Content = "Obtained log";
        }

        public static void obtainEdsmLogs(StarMapConfiguration starMapConfiguration, string commanderName, IProgress<string> progress)
        {
            StarMapService starMapService = new StarMapService(starMapConfiguration.apiKey, commanderName);
            Dictionary<string, StarMapLogInfo> systems = starMapService.getStarMapLog();
            Dictionary<string, string> comments = starMapService.getStarMapComments();
            foreach (string system in systems.Keys)
            {
                progress.Report(system);
                StarSystem CurrentStarSystem = StarSystemSqLiteRepository.Instance.GetOrCreateStarSystem(system, false);
                CurrentStarSystem.visits = systems[system].visits;
                CurrentStarSystem.lastvisit = systems[system].lastVisit;
                if (comments.ContainsKey(system))
                {
                    CurrentStarSystem.comment = comments[system];
                }
                StarSystemSqLiteRepository.Instance.SaveStarSystem(CurrentStarSystem);
            }
        }

        private void eddiScriptsUpdated(object sender, RoutedEventArgs e)
        {
            updateEddiConfiguration();
        }

        private void eddiScriptsUpdated(object sender, DataTransferEventArgs e)
        {
            updateEddiConfiguration();
        }

        private void editScript(object sender, RoutedEventArgs e)
        {
            Script script = ((KeyValuePair<string, Script>)((Button)e.Source).DataContext).Value;
            EditScriptWindow editScriptWindow = new EditScriptWindow(eddiConfiguration.Scripts, script.Name);
            editScriptWindow.ShowDialog();
            scriptsData.Items.Refresh();
        }

        private void resetScript(object sender, RoutedEventArgs e)
        {
            Script script = ((KeyValuePair<string, Script>)((Button)e.Source).DataContext).Value;
            script.Value = null;
            eddiScriptsUpdated(sender, e);
            scriptsData.Items.Refresh();
        }
    }

    public class ValidIPARule : ValidationRule
    {
        private static Regex IPA_REGEX = new Regex(@"^[bdfɡhjklmnprstvwzxaɪ˜iu\.ᵻᵿɑɐɒæɓʙβɔɕçɗɖðʤəɘɚɛɜɝɞɟʄɡ(ɠɢʛɦɧħɥʜɨɪʝɭɬɫɮʟɱɯɰŋɳɲɴøɵɸθœɶʘɹɺɾɻʀʁɽʂʃʈʧʉʊʋⱱʌɣɤʍχʎʏʑʐʒʔʡʕʢǀǁǂǃˈˌːˑʼʴʰʱʲʷˠˤ˞n̥d̥ŋ̊b̤a̤t̪d̪s̬t̬b̰a̰t̺d̺t̼d̼t̻d̻t̚ɔ̹ẽɔ̜u̟e̠ël̴n̴ɫe̽e̝ɹ̝m̩n̩l̩e̞β̞e̯e̘e̙ĕe̋éēèȅx͜xx͡x↓↑→↗↘]+$");

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value == null)
            {
                return ValidationResult.ValidResult;
            }
            string val = value.ToString();
            if (IPA_REGEX.Match(val).Success)
            {
                return ValidationResult.ValidResult;
            }
            else
            {
                return new ValidationResult(false, "Invalid IPA");
            }
        }
    }
}
