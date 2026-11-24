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

