# Embedded font provenance

The UI adds unmodified static TrueType files from the official author repositories at the revisions below. Only the regular and semibold/bold faces needed for the interface are embedded. Existing Inter continues to come from the locked Avalonia.Fonts.Inter package. No font installation or runtime font download is performed.

- Outfit: https://github.com/Outfitio/Outfit-Fonts/tree/902773808eb372f70fb34e8946dd1ffe604efc79/fonts/ttf — Outfit-Regular.ttf and Outfit-SemiBold.ttf.
- Space Grotesk: https://github.com/floriankarsten/space-grotesk/tree/03507d024a01282884232081fc6011c09ff4e849/fonts/ttf/static — SpaceGrotesk-Regular.ttf and SpaceGrotesk-Bold.ttf.
- Sora: https://github.com/sora-xor/sora-font/tree/7f9a9c5d0ccd1c099cfac420aa27133df1c5fdc4/fonts/ttf — Sora-Regular.ttf and Sora-SemiBold.ttf (stable directory, not the beta subdirectory).

Each accompanying family-OFL.txt is copied unchanged from that exact repository revision. The full SIL Open Font License texts are embedded and readable under Settings > About > Additional bundled font licenses, and included here in source. The six new TTFs total 557,648 bytes before compression. Variable-font experiments were not retained in the app or release source; actual regular and semibold/bold glyph faces are validated in the regression suite.

## SHA-256
c676351bf8576b9aba743cd5eaa8c0e7ee0d51f805d720447b4df4ddb6a2e416  Outfit-OFL.txt
3b64ac4f6ab6a8eebddd4b0bc03c811c43602e11e176382ab0ee6be615ab861b  Outfit-Regular.ttf
bf2e1d2a6ec2a67952e8b36edd2b2bb9f340c0cdd10b0ad5145b4dbbc1339608  Outfit-SemiBold.ttf
ba0b9729c9428ba79a0459ab8ec575791b51509dbec213e383d0316d37fec299  Sora-OFL.txt
517e945dedbeeb8d700ccae77d189a6ef2a01f6dcc95ba5d032ef9a30f7f0de9  Sora-Regular.ttf
b02621d4009da9a18eb8a5cbe277f084eb9c3f065ba08a6e77769e81863becf2  Sora-SemiBold.ttf
7209bbb75fc0f5c546a5f5773b0db74ffc6abf04c2148c1105cace5765a96bdb  SpaceGrotesk-Bold.ttf
564ce565c371c5e5bbf286006565a7c9aa55a9f56e7ca58d56e05d649dd61a72  SpaceGrotesk-OFL.txt
5ede28c4425f3fe4830c8f4754b39e9a87a93d0c3baa5e0a9924532aaa8a98bd  SpaceGrotesk-Regular.ttf


## Version 0.7.14 expansion

Five further families use ten unmodified static TTF files (3,610,000 bytes), plus their complete unchanged licenses. All fonts/licenses are embedded. No variable-font defaults or subsetting is introduced.

- Lato: google/fonts at 06bfe0d62a98375cb09c942fa18e68bd32cff057, ofl/lato, Regular + SemiBold and OFL.txt.
- Montserrat: JulietaUla/Montserrat at 555facfb2a18c72c3c0380f0d9c0f060453a9058, fonts/ttf, Regular + SemiBold and root OFL.txt.
- Source Sans 3: adobe-fonts/source-sans at 87b37a2daaed80fcb8e8ccb0085c4d72ddade12e, TTF, Regular + Semibold and root LICENSE.md (copied as SourceSans3-OFL.txt).
- IBM Plex Sans: IBM/plex at 2e90fe366fdc27c12682db13e5aa20ded3d90908, packages/plex-sans/fonts/complete/ttf, Regular + SemiBold and packages/plex-sans/LICENSE.txt (copied as IBMPlexSans-OFL.txt).
- Atkinson Hyperlegible: google/fonts at 06bfe0d62a98375cb09c942fa18e68bd32cff057, ofl/atkinsonhyperlegible, Regular + Bold and OFL.txt.

Full source mappings: provenance-0.7.14.json. These public repository paths are metadata, not runtime requests. Below are SHA-256 hashes of the exact added assets.
5a3b0c8cc8ca545155150b4512a1fa248298df121c50d6557e651e61fbdab92f  AtkinsonHyperlegible-Bold.ttf
f32d22b3908fcad2c86a74000614ec22e6a7f66ea7e867e616026a27aebdc143  AtkinsonHyperlegible-OFL.txt
7fb917c89019896d0b52ee84b7cbb3304c18cb90b19a62f5e32712bd23e97669  AtkinsonHyperlegible-Regular.ttf
7e6b2818edbd8f6a01ae80641cc8f16a51080d08fb4e532be3a0b6f74adb07da  IBMPlexSans-OFL.txt
b47ebd72b5ce53b60f909033ff84d6fc12358cc147e2981f8c321ad3f36246b6  IBMPlexSans-Regular.ttf
c51119b25f4a296da2b66edf3201b9aa1735091ed7efc1fca0313346f3851a4d  IBMPlexSans-SemiBold.ttf
74ba064d03f1f1c4a952da936c3eb71866c34404916734de3cae73b34357e59e  Lato-OFL.txt
d636e4683231f931eda222d588e944d082bfd3bdba02f928bee461c0f185b251  Lato-Regular.ttf
71b8b7decbe75a881ed267be539d402bd1e9420b799658aada4e0d1bd5af803c  Lato-SemiBold.ttf
8b7141c03fa4f8d44e6345d5d4931709290f0f67875e452e95ac1fd3a027802e  Montserrat-OFL.txt
3e8abe50c44c82e2242e97d1ec8c0d385c4890cdc50447bcdb8605c81a38cfb2  Montserrat-Regular.ttf
b4e1563393d73fdff491a869441245aef31add2ec03d9c97a6dae4de07c52fd0  Montserrat-SemiBold.ttf
56af9b9c6715597e458284a474dc118a50a4150e9d547c70f7b4a33c3e6a9328  SourceSans3-OFL.txt
4644c81b86ec9caaa76b634889968ed3c4f4f52f054855933acc7c2b21e53b0f  SourceSans3-Regular.ttf
a3f4f8dcf343a8f24dc61951de93f3ba1558b15cd250ba24af8a40e957081b7d  SourceSans3-Semibold.ttf

