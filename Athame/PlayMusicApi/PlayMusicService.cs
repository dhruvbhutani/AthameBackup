﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Athame.PluginAPI.Downloader;
using Athame.PluginAPI.Service;
using GoogleMusicApi.Common;

namespace Athame.PlayMusicApi
{
    public class PlayMusicService : MusicService
    {
        private const string GooglePlayHost = "play.google.com";
        private MobileClient client = new MobileClient();
        private PlayMusicServiceSettings settings = new PlayMusicServiceSettings();

        public override async Task<AuthenticationResponse> LoginAsync(string username, string password)
        {
            await client.LoginAsync(username, password);
            return new AuthenticationResponse
            {
                Token = client.Session.MasterToken,
                UserIdentity = username,
                UserName = client.Session.FirstName + " " + client.Session.LastName + " (" + username + ")"
            };
        }

        public override async Task<bool> RestoreSessionAsync(AuthenticationResponse response)
        {
            return await client.LoginWithToken(response.UserIdentity, response.Token);
        }

        public override bool RestoreSession(AuthenticationResponse response)
        {
            throw new NotImplementedException();
        }

        public override void ClearSession()
        {
            // reinit client to clear stored session
            client = new MobileClient();
            settings.Response = null;
        }

        private Artist CreateArtist(GoogleMusicApi.Structure.Track track)
        {
            return new Artist
            {
                Id = track.ArtistIds[0],
                Name = track.Artist
            };
        }

        private Artist CreateArtist(GoogleMusicApi.Structure.Album album)
        {
            return new Artist
            {
                Id = album.ArtistId[0],
                Name = album.AlbumArtist
            };
        }

        private Track CreateTrack(GoogleMusicApi.Structure.Track gpmTrack)
        {
            return new Track
            {
                Artist = CreateArtist(gpmTrack),
                DiscNumber = gpmTrack.DiscNumber,
                Genre = gpmTrack.Genre,
                Title = gpmTrack.Title,
                Year = gpmTrack.Year,
                TrackNumber = gpmTrack.TrackNumber,
                Id = gpmTrack.StoreId,
                // AFAIK tracks returned will always be downloadable or else the server will give a 404/403/400
                IsDownloadable = true
            };
        }

        private Album CreateAlbum(GoogleMusicApi.Structure.Album gpmAlbum)
        {
            var a = new Album
            {
                Artist = CreateArtist(gpmAlbum),
                CoverUri = new Uri(gpmAlbum.AlbumArtRef),
                Title = gpmAlbum.Name,
                Tracks = new List<Track>()
            };
            if (gpmAlbum.Tracks != null)
            {
                foreach (var track in gpmAlbum.Tracks)
                {
                    var cmTrack = CreateTrack(track);
                    cmTrack.Album = a;
                    ((List<Track>)a.Tracks).Add(cmTrack);
                }

            }
            return a;
        }

        private Album CreateAlbum(GoogleMusicApi.Structure.Album album, List<GoogleMusicApi.Structure.Track> tracks)
        {
            var a = CreateAlbum(album);
            a.Tracks = new List<Track>(from t in tracks select CreateTrack(t));
            return a;
        }

        public override async Task<TrackFile> GetDownloadableTrackAsync(Track track)
        {
            // Only property we need to set is Track.StoreId (see Google.Music/GoogleMusicApi.UWP/Requests/Data/StreamUrlGetRequest.cs:32)
            var streamUrl = await client.GetStreamUrlAsync(new GoogleMusicApi.Structure.Track { StoreId = track.Id });
            if (streamUrl == null)
            {
                throw new InvalidSessionException("Play Music: Stream URL unavailable. Check your subscription is active then try again.");
            }
            // Unfortunately I have forgotten the various stream qualities available on Play Music because my subscription ran out,
            // so I will set the bitrate to -1, i.e. unknown
            // What is known is that all streams are MP3, so this should work.
            return new TrackFile
            {
                BitRate = -1,
                DownloadUri = streamUrl,
                FileType = MediaFileTypes.Mpeg3Audio,
                Track = track
            };
        }

        public override Task<Playlist> GetPlaylistAsync(string playlistId)
        {
            throw new NotImplementedException();
        }

        public override UrlParseResult ParseUrl(Uri url)
        {
            if (url.Host != GooglePlayHost)
            {
                return null;
            }
            var hashParts = url.Fragment.Split('/');

            if (hashParts.Length <= 2)
            {
                return null;
            }
            var type = hashParts[1];
            var id = hashParts[2];
            var result = new UrlParseResult { Id = id, Type = MediaType.Unknown, OriginalUri = url };
            switch (type)
            {
                case "album":
                    result.Type = MediaType.Album;
                    break;

                case "artist":
                    result.Type = MediaType.Artist;
                    break;

                // Will auto-playlists actually be interchangeable with user-generated playlists?
                case "pl":
                case "ap":
                    result.Type = MediaType.Playlist;
                    break;

                default:
                    result.Type = MediaType.Unknown;
                    break;
            }
            return result;
        }

        public override Task<SearchResult> SearchAsync(string searchText, MediaType typesToRetrieve)
        {
            throw new NotImplementedException();

        }

        public override async Task<Album> GetAlbumAsync(string albumId, bool withTracks)
        {
            try
            {
                // Album should always have tracks
                var album = await client.GetAlbumAsync(albumId, includeDescription: false);
                return CreateAlbum(album);
            }
            catch (HttpRequestException ex)
            {
                // Just uhhhh
                throw new ResourceNotFoundException(ex.Message);
            }
        }

        public override async Task<Track> GetTrackAsync(string trackId)
        {
            var track = await client.GetTrackAsync(trackId);
            return CreateTrack(track);
        }

        public override string Name => "Google Play Music";

        public override bool IsAuthenticated => client.Session != null && client.Session.IsAuthenticated;

        public override Control GetSettingsControl()
        {
            return new PlayMusicSettingsControl(settings);
        }

        public override StoredSettings Settings
        {
            get
            {
                return settings;
            }
            set { settings = (PlayMusicServiceSettings)value ?? new PlayMusicServiceSettings(); }
        }

        public override Uri[] BaseUri => new[] { new Uri("http://" + GooglePlayHost), new Uri("https://" + GooglePlayHost) };

        public override AuthenticationMethod AuthenticationMethod => AuthenticationMethod.UsernameAndPassword;

        public override AuthenticationFlow Flow => new AuthenticationFlow
        {
            SignInInformation =
                "Enter your Google account email and password. If you use two-factor authentication, you must set an app password:",
            LinksToDisplay = new[]
            {
                new SignInLink{DisplayName = "Set an app password", Link = new Uri("https://security.google.com/settings/security/apppasswords")},
                new SignInLink{DisplayName = "Forgot your password?", Link = new Uri("https://accounts.google.com/signin/recovery")}
            }
        };
    }
}
