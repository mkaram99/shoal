# Working on the web Shoal

The whole app is `index.html` -- markup, styles and all the JavaScript in one file, with `sw.js`,
`manifest.webmanifest` and `icons/` beside it. There is no build step: what is in the repo is what is served.

## Pushing publishes

**A push here is a release.** GitHub Pages deploys from the repo automatically, so `git push` puts the change
in front of everyone within a minute or two. There is no staging copy. Check the change in a browser before
pushing, not after.

It is served from two places and **they drift**:

- `https://mkaram99.github.io/shoal/` -- GitHub Pages, deploys itself from this repo on push.
- `https://apps.theinvestorsnest.com/shoal/` -- a copy on the VPS, which only changes when somebody puts it
  there. It was two commits behind for a month before anyone noticed.

To bring the VPS copy up to date, send `index.html` by itself:

    scp -i ~/.ssh/redwood_deploy index.html ubuntu@apps.theinvestorsnest.com:/tmp/shoal-index.html
    # on the server:
    sudo cp /srv/shoal/index.html /var/backups/shoal/index-before-$(date +%Y%m%d-%H%M%S).html
    sudo install -o root -g root -m 644 /tmp/shoal-index.html /srv/shoal/index.html

Do **not** use `ShoalNative/site/deploy_shoal.sh` for a page change: it replaces all of `/srv/shoal` and
insists the tarball also carry the app downloads, so it would quietly republish those too.

To find out which revision is live, download it and ask git, rather than comparing sizes:

    git hash-object live.html        # against: git rev-parse <rev>:index.html

## Android gets this page too

`CLAUDE-PROJECTS/shoal-android` is a TWA wrapper that loads `mkaram99.github.io`, so a change here reaches
the Play package with no rebuild. It holds no Shoal code of its own.

The phone and headset apps in `CLAUDE-PROJECTS/ShoalNative` are a **separate C++ implementation** that shares
nothing with this one -- down to its own copies of the constants (`ATLAS = 4096, GRID = 16` here mirror
`shoal.h` there). A feature wanted in both has to be written twice, in two languages.

## Things already learnt here

- **`MAX_PHOTOS` is 1,024** (`TILES * PAGES`, 256 x 4). Beyond that, adding photos reports them as over the
  limit rather than failing.
- **A phone's picker caps one selection** -- 500 on Android -- and **no web API can ask what the cap is**, or
  tell that a selection was truncated. `<input multiple>` has no limit attribute. So the page cannot request
  "as many as allowed"; it can only say that picking again adds more, which `addPhotos` already supports:
  it appends and skips anything whose signature is already there.
- **The service worker is network-first for the page shell** and asks the server every visit, so a change
  shows up on the next load. Do not "fix" a stale page by bumping the cache name without checking that.
- **`showDirectoryPicker` and `webkitdirectory`** only help on desktop; Android browsers ignore them.

## Apple

This is the only Shoal an iPhone or iPad can run: the native apps are C++ and OpenXR with an Android shell,
and there is no iOS build nor a cheap way to make one.

It should work there already, and two details are what make it so -- neither is incidental, so take care
with both:

- **`decode()` falls back from `createImageBitmap` to an `<img>` with `.decode()`.** That fallback is what
  reads HEIC, which is what iPhones shoot: Safari decodes it through the system codec in an `<img>` even
  where `createImageBitmap` will not. The "HEIC isn't supported" line only comes from the catch, so Safari
  users never see it and Android Chrome users get advice that is true for them.
- **`DeviceOrientationEvent.requestPermission()` is asked for** before listening. Without that gate, iOS 13
  and later silently never fire the event and "look around by moving the phone" does nothing at all.

Not yet confirmed on an Apple device: a browser here is Chromium, which is the one that cannot read HEIC,
so testing it on this machine proves nothing. What else differs there: Safari has no WebXR, so Enter VR is
not offered, and it may clear IndexedDB after about a week for a site that has not been added to the home
screen, so a shoal need not persist.
