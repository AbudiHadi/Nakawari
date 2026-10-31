# Third-party notices

Nakawari is MIT-licensed (see `LICENSE`). It stands on the work below. Each entry says whether the
component is **bundled** in this repository or the installer, or **fetched at run time** from its
official source on your request, or only **called** as a separate program you install yourself.

| Component | Author | License | How Nakawari uses it |
|---|---|---|---|
| [MFG Unlock addon](https://github.com/mavismmg/MFGAdaUnlock-RenoDx) (`renodx-mfgunlock.addon64`) | ImDreamt (original, copyright holder), maintained fork by mavismmg, with dashdogy's research | MIT | Fetched at run time from its GitHub releases. Not in this repository. |
| [ReShade](https://reshade.me) ([source](https://github.com/crosire/reshade)) | crosire | BSD-3-Clause | Fetched at run time: the official add-on installer is downloaded from reshade.me and `ReShade64.dll` is taken out of it. Not in this repository. |
| [SCSKiller](https://github.com/BlueHeisenberg/SCSKiller) | BlueHeisenberg | GPL-3.0 | Called as a separate program through its own command line. Nothing of it is bundled or copied. |
| [Velopack](https://github.com/velopack/velopack) | Velopack contributors | MIT | Bundled (NuGet package). Provides the installer, Start-menu entry and self-update. |
| [Varela Round](https://fonts.google.com/specimen/Varela+Round) | The Varela Round Project Authors | SIL Open Font License 1.1 | Bundled as `src/Assets/Fonts/VarelaRound-Regular.ttf`. License text in `src/Assets/Fonts/OFL-VarelaRound.txt`. |
| [.NET 10 and WPF](https://github.com/dotnet/wpf) | Microsoft and contributors | MIT | Bundled runtime (self-contained build). |
| [NVIDIA Streamline](https://github.com/NVIDIA-RTX/Streamline) runtime and DLSS Frame Generation | NVIDIA | NVIDIA's own license terms | Only if you turn on the optional 50-series kit: the production runtime is fetched from NVIDIA's official v2.14.1 release and every file must carry a valid NVIDIA signature. DLSS-G DLLs are otherwise taken only from games already installed on your PC. Nothing from NVIDIA is in this repository or the installer. |

## Velopack (MIT)

Copyright (c) 2021 Caelan Sayler and Velopack contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions: The above copyright notice and this
permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND.

## ReShade (BSD-3-Clause)

Copyright (c) 2014 Patrick Mours. All rights reserved. Redistribution and use in source and binary
forms, with or without modification, are permitted provided that the conditions of the BSD 3-Clause
License are met. See https://github.com/crosire/reshade/blob/main/LICENSE.md.

## MFG Unlock addon (MIT)

Copyright (c) 2026 ImDreamt. The release Nakawari downloads is the maintained fork by mavismmg, which keeps that MIT license. See
https://github.com/ImDreamt/MFGAdaUnlock-RenoDx and https://github.com/mavismmg/MFGAdaUnlock-RenoDx.

## Trademarks

NVIDIA, DLSS, GeForce and RTX are trademarks of NVIDIA Corporation. Nakawari is not affiliated
with or endorsed by NVIDIA, ReShade, RenoDX or SCSKiller.
