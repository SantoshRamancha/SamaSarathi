# Manual test checklist (phases 1–4, destination search, routing and weather)

Walk through these in order: they follow one driver from sign-up to approval, then the rider app.
Tick each box as you confirm it. Anything that doesn't match, note the step number and take a screenshot.

## Before you start

| What | Where | Started with |
|---|---|---|
| API | http://localhost:5299 (docs at `/scalar`) | `dotnet run --project backend/src/Samasarathi.Api --launch-profile http` |
| Driver app | Chrome window from `flutter run -d chrome` in `mobile/driver_app` | |
| Rider app | Chrome window from `flutter run -d chrome` in `mobile/rider_app` | |
| Redis (live driver positions) | `docker compose up -d` | needed for section H |
| Admin portal | http://localhost:5310 | `dotnet run --project admin/Samasarathi.Admin --launch-profile http` |

- The OTP is always **123456**. Any 10-digit number starting with 6–9 works.
- Admin number: **9999999999**.
- Use a **new** driver number for this run (e.g. 9000000001) so you start from a blank registration.
- A new code can be requested only every 30 seconds per number.

---

## A. Driver registers (driver app)

- [ ] **A1** Enter `12345` → "Enter your 10-digit mobile number".
- [ ] **A2** Enter your new number → code screen shows "the code is 123456" hint and a 30 s resend timer.
- [ ] **A3** Enter `111111` → "Incorrect or expired code". Then `123456` → "Become a driver" with 4 steps.
- [ ] **A4** *Your details*: enter a name → Save → step 1 ticked, Vehicle opens.
- [ ] **A5** *Vehicle*: type `HELLO` → "Enter a valid vehicle number, e.g. KA01AB1234".
- [ ] **A6** Type `TS09EA4321` (already used by a test driver) → "This vehicle is already registered with another driver."
- [ ] **A7** Type a new number, e.g. `KA05 MN 4321`, pick Bike/Auto/Car, Save → Documents opens.
- [ ] **A8** *Documents*: upload a photo, licence, RC and ID (JPG, PNG or PDF, max 5 MB). Each shows a tick and the file name.
      After the 4th, the Rate step opens by itself. Try one file over 5 MB → "File is larger than 5 MB".
- [ ] **A9** Tap **Replace** on one document and pick a different file → new file name shown.
- [ ] **A10** *Your rate*: type `500` → "Enter a rate between ₹3 and ₹100". Type `12` → sample shows
      **₹120** normal (10 km) and **₹300** rainy evening (60 min). Save.
- [ ] **A11** Reload the page (F5) → still logged in, all 4 steps ticked, button says "Submit for verification".
- [ ] **A12** Submit → "Under verification" screen with 5 pending checks and your details.
      The ⟳ refresh and log-out icons are top-right (in debug builds the red DEBUG ribbon partly covers log-out).

## B. Admin reviews and asks for changes (portal)

- [ ] **B1** Open http://localhost:5310 → redirected to the login page.
- [ ] **B2** Log in with your **driver's** number → "This number does not have admin access."
- [ ] **B3** "Change number" → `9999999999` → code box gets the cursor → `123456` → Driver verification page.
- [ ] **B4** *In review* tab: your driver is at the **bottom** (longest waiting first), "0 / 5 passed".
      Tabs show counts.
- [ ] **B5** Search your vehicle number (e.g. `MN4321`) → only your driver, with a Status column. "Clear" resets.
- [ ] **B6** Open your driver → details on the left; documents on the right. Click each tab
      (Photo, Driving licence, Vehicle RC, ID proof). Images show inline, PDFs in the PDF viewer.
      "Open in new tab" works.
- [ ] **B7** *Driving licence* check → **Fail** with no note → "Add a note explaining why the check failed."
- [ ] **B8** Type a note (e.g. `Licence photo is blurry`) → **Fail** → red "Failed" badge,
      "Recorded by +919999999999 on …".
- [ ] **B9** **Approve driver** is disabled ("Every check must pass before approval").
      *Request changes* box is pre-filled with "Driving licence: Licence photo is blurry".
- [ ] **B10** Clear the box, type `no`, Send → error asking for a longer reason (min 5 characters). Restore a real message → **Send back to driver**
      → yellow "Changes requested" badge and "Sent back to the driver: …" banner.
- [ ] **B11** Back to drivers → your driver is now under the *Changes requested* tab.

## C. Driver fixes and resubmits (driver app)

- [ ] **C1** Tap ⟳ (or reload) → registration screen opens again with a red **Changes needed** box showing
      the admin's message.
- [ ] **C2** Open *Documents* → **Replace** the driving licence → **Submit for verification** → "Under verification" again.

## D. Admin approves (portal)

- [ ] **D1** *In review* tab → your driver is back. Open them: all 5 checks are *Pending* again, and
      **Earlier submissions** at the bottom lists the old failed licence check.
- [ ] **D2** Click **Pass** on one check, then **Undo** → back to Pending.
- [ ] **D3** Pass all 5 → **Approve driver** becomes enabled → click → confirm dialog → OK →
      green "Approved by +919999999999 on …".
- [ ] **D4** *Approved* tab lists your driver with "5 / 5 passed".
- [ ] **D5** Click **Log out** (top right) → login page. Close the tab, reopen the portal → asks you to log in
      (admin sessions end with the tab, and expire after 12 hours).

## E. Driver sees approval (driver app)

- [ ] **E1** Tap ⟳ → **Approved**: "You are verified. You can go online and start taking rides." (with a map and **Go online** button; see section H) All 5 checks ticked.
- [ ] **E2** Close the Chrome window and run the app again → still logged in, still Approved (no OTP needed).
- [ ] **E3** Log out → login screen.

## F. Rider (rider app)

- [ ] **F1** Log in with a **new** number → "What should drivers call you?"
- [ ] **F2** Tap the log-out icon on that screen → back to login (lets someone who typed the wrong number start over).
- [ ] **F3** Log in again → enter your name → home screen "Hi <name> 👋".
- [ ] **F4** **Check a fare** with defaults (₹10/km, 10 km, 25 min) → **₹100**, charged by distance.
- [ ] **F5** Turn on **Raining**, set 60 min → **₹250**, "charged by travel time".
- [ ] **F6** Rain off, **Peak hour** on, 20 min → **₹100**: time mode never charges less than the distance fare.
- [ ] **F7** Reload → still logged in. Log out → login screen.
- [ ] **F8** Log in to the rider app with your **driver's** number → works: the same person can be both, and each app keeps its own login.

## G. Optional: API checks in Scalar (http://localhost:5299/scalar)

- [ ] **G1** `GET /api/admin/drivers` with no token → **401**.
- [ ] **G2** Get a driver token (`/api/auth/otp/request` + `/otp/verify` with role `driver`) and call
      `GET /api/admin/drivers` with it → **403**.
- [ ] **G3** `POST /api/admin/drivers/{id}/approve` on a driver still in review with pending checks → **400**
      "These checks have not passed yet: …".

## H. Maps: going online and nearby drivers (phase 3)

Needs an **approved** driver (finish section D first) and Docker running (Redis). Open the driver app and the rider
app in **two Chrome windows** side by side. Allow the browser's location prompt in both (if you block it, you can
still test: in debug builds **tapping the map moves "you"**, shown by the amber hint on the map).

- [ ] **H1** Driver app (approved driver): status card says "You are verified…" and a map with a blue dot appears, with **Go online**.
- [ ] **H2** Rider app, home screen: a map with a blue dot and "No drivers nearby right now".
- [ ] **H3** Driver: tap the map near the rider's blue dot (or leave it if both are at your real location) → **Go online**
      → text changes to "You are online…" and the button becomes **Go offline**.
- [ ] **H4** Rider: within ~5 seconds a driver marker appears and the text says "1 driver nearby". Hover the marker → vehicle, ₹/km, distance.
- [ ] **H5** Driver: tap a different spot on the map → rider's marker moves within ~5 seconds.
- [ ] **H6** Driver: tap a spot **more than 3 km** from the rider → marker disappears, "No drivers nearby right now".
- [ ] **H7** Driver: tap back near the rider, then **Go offline** → rider's marker disappears within ~5 seconds.
- [ ] **H8** Driver: Go online, then close the driver tab. After about a minute the rider no longer sees the driver (no updates → treated as gone).
- [ ] **H9** Driver: Go online, reload the page (F5) → still shows "You are online" and keeps reporting.
- [ ] **H10** Driver: Go online, then **Log out** → rider stops seeing the driver.
- [ ] **H11** Block the browser's location permission for the rider app and reload → red "Location permission was denied…" message with **Try again**.
- [ ] **H12** A driver who is not approved has no map and no Go online button (check with a driver still *Under verification*).
- [ ] **H13** API check: `POST /api/drivers/me/online` with a driver token that is **not** approved → **409**.

---

## I. Requesting a ride and taking the trip (phase 4)

First apply the new database migration and **restart the API** (it adds the ride columns and the "one active ride per
rider / per driver" rules):

```bash
dotnet ef database update -p backend/src/Samasarathi.Infrastructure -s backend/src/Samasarathi.Api
```

You need: an **online approved driver** (section H) and a rider logged in (section F). Rider app and driver app in two
Chrome windows. Rider: put yourself near the driver (tap the map in debug, or both at your real location).

- [ ] **I1** Rider home: the "Where are you going?" card is now enabled (it was greyed out before). Tap it → **Book a ride** screen with a map.
- [ ] **I2** Tap the map somewhere ~5 km away → a red pin appears, the map zooms to show both points, step 1 ticks.
- [ ] **I3** The driver is listed under the map ("Auto · ₹12 per km · 0.1 km away"). Tap it (or its marker) → step 2 ticks, an **Estimated fare** card appears. Check: fare ≈ distance × the driver's rate (distance is ~30% longer than the straight line).
- [ ] **I4** Tap the red pin's spot very close to you (under 200 m) → "Pick a drop point a little further from your pickup." Pick a far spot again.
- [ ] **I5** Press **Request ride** → *Your ride* screen: "Waiting for <driver> to accept…" with a countdown (45 s).
- [ ] **I6** Driver window (within ~3 s): a **New ride request** card with rider name, pickup distance, trip km/min, fare, countdown, and green + red pins on the map.
- [ ] **I7** Driver: **Decline** → card disappears. Rider (within ~3 s): "The driver can't take this ride" + **Back to home**.
- [ ] **I7b** After the decline, the rider's screen has **Choose another driver** (main button) and **Back to home**. Tap **Choose another driver** → the booking screen reopens with the **same pickup and drop-off and the route already set**, and the driver who declined is **not** in the list (other online drivers are). Pick another driver → fare card → request again. Same after a timeout (I12) and after a driver cancel (I13); after a rider cancel the same button reopens booking with all drivers listed. **Back** on that screen returns to home.
- [ ] **I8** Request again. Driver: **Accept** → card becomes "Ride accepted" with the rider's name and phone. Rider: "Driver accepted!" with driver name, vehicle, registration number and phone.
- [ ] **I9** Home (rider): the driver's marker is gone from the nearby map (they're busy), and a **Ride in progress** card replaces "Where are you going?". Tap it → back to the ride screen.
- [ ] **I10** Reload the rider page (F5) → home shows **Ride in progress** again (the ride is remembered by the server).
- [ ] **I11** Rider: **Cancel ride** → "Cancelled by rider". Driver (within ~3 s): "The ride request ended…" note. Both can book / go on again.
- [ ] **I12** Request again and **don't answer** for 45 s → rider: "The driver didn't answer in time". Driver: the card disappears.
- [ ] **I13** Request, driver accepts, then driver taps **Cancel ride** → rider sees "Cancelled by driver".
- [ ] **I14** While a request is waiting, the driver's **Go offline** button is disabled ("Finish or cancel your ride first").
- [ ] **I15** Open a second rider (new number, e.g. 9000000002, in another browser profile) while the first rider's request is waiting → that driver is **not** on the second rider's map or list.
- [ ] **I16** API checks in Scalar: `POST /api/rides` with a rider token for a driver who is offline → **409** "This driver isn't available any more"; `GET /api/rides/{id}` of someone else's ride → **404**.

- [ ] **I17** Driver (ride accepted): button **I've arrived** → card becomes "Waiting for <rider>" with **Start trip**. Rider (within ~3 s): "Your driver has arrived".
- [ ] **I18** Driver: **Start trip** → "Trip in progress" with **Complete trip**, and the **Cancel ride** button is gone. Rider: "Trip in progress", and its **Cancel ride** button is gone too.
- [ ] **I19** Before arriving, driver-side **Start trip** isn't offered (you can't skip a step). API check: `POST /api/drivers/me/ride/{id}/start` on an accepted ride → **409**.
- [ ] **I20** Driver: **Complete trip** → "Trip complete — Collect ₹…" with the km and minutes, and a **Done** button. Rider: "Trip complete" with the fare, time, driver details and "Pay directly (cash or UPI)".
- [ ] **I21** Fare check: with a normal (distance-priced) ride the final fare equals the estimate however long the trip took.
- [ ] **I22** Driver: tap **Done** → back to the map; **Go offline** works again; the driver is on riders' maps again. Rider: **Back to home** → no "Ride in progress" card.
- [ ] **I23** Rider app, while the driver is on the way (accepted): the title shows "Driver is on the way · X km away" and the driver's marker shows the bike / auto / car icon of their vehicle.
- [ ] **I24** Driver with a waiting request or active ride: `POST /api/drivers/me/offline` → **409** "Finish or cancel your current ride before going offline."
- [ ] **I25** New request arrives at the driver app → a short alert sound / vibration (phones only; browsers stay silent).

## J. Destination search (needs internet; uses free OpenStreetMap search)

Restart the API first (new endpoint + config). **Use the rider app** (not the driver app).

- [ ] **J0** Rider home: a white search bar floats at the top of the map: "Where to? Search for a place" (it says "Finding your location…" and is disabled until your location is known). The amber debug hint is now at the bottom-left of the map.
- [ ] **J1** Tap the search bar → **Book a ride** opens with the cursor already in the search box: "Search for a place (e.g. HITEC City metro)". (The "Where are you going?" card below the map opens the same screen without the keyboard.)
- [ ] **J2** Type 2 letters → nothing happens. Type a third letter (e.g. `hit`) and pause → a progress bar, then up to 5 matching places (name + full address).
- [ ] **J3** Tap a result → the box shows its name, the list closes, a red pin appears on it, the map zooms to show both points, and step 1 says "Going to <name>".
- [ ] **J4** Pick a driver → the fare card appears for that trip (as in section I).
- [ ] **J5** Tap somewhere else on the map → the red pin moves and step 1 says "Drop-off set on the map". The search box text stays (it is just a helper).
- [ ] **J6** Clear the box with the **×** → results and messages disappear; the drop-off pin stays.
- [ ] **J7** Type nonsense (e.g. `zzqxqxqx`) → "No places found. Try another name, or tap the map."
- [ ] **J8** Type fast (`hitec city metro`, one letter at a time) → only ONE search is sent after you stop (check the API console / network tab: no request per letter).
- [ ] **J9** Search for the same thing again → instant (cached for 10 minutes).
- [ ] **J10** Turn off your internet (or block `nominatim.openstreetmap.org`), search a new place → red text "Place search isn't available right now. You can tap the map instead." Tapping the map still works.
- [ ] **J11** Local places rank first: search `metro` while your position is in Hyderabad → results are in Hyderabad, not other cities.
- [ ] **J12** API check in Scalar with a rider token: `GET /api/places/search?query=a` → **400** (too short); with no token → **401**; with a driver token → **403**.

---

## K. Real routes, peak hours and rain pricing (needs internet)

Restart the API first (new services + config). No database migration. Rider app → Book a ride → pick a destination ~10 km away and an online driver.

- [ ] **K1** Pick a destination: a **blue line** follows the real roads from you to the drop-off (not a straight line), and the map zooms to fit it.
- [ ] **K2** The fare card shows the road distance and minutes. Compare with Google Maps for the same trip: the km should be close (the minutes are "free-flow", without live traffic).
- [ ] **K3** Normal time and dry weather → under the fare: "Charged by distance (normal traffic, dry weather)." Fare = km × the driver's rate.
> **Expected with the default 25 km/h normal speed:** peak hour alone or rain alone switches the label to "charged by travel time" but the
> upfront fare usually stays the same as the distance fare (slowed trips are still faster than 25 km/h). Only peak + rain together raise it
> (e.g. Charminar → Secunderabad Station at Rs10/km: Rs116 normally, Rs116 peak, Rs116 rain, about Rs125 both). That is by design, not a bug.
> To see a fare rise from one condition, add `"Pricing": { "NormalSpeedKmph": 40 }` to `appsettings.Development.json` while testing
> (about Rs153 peak, Rs133 rain, Rs200 both for that trip) and remove it afterwards.

- [ ] **K4** **Peak hour:** stop the API, set the current time inside a window, e.g. in `backend/src/Samasarathi.Api/appsettings.Development.json` add `"PeakHours": { "Windows": [ "00:00-23:59" ], "WeekdaysOnly": false }`, restart the API, get the quote again → an orange note "Charged by travel time because of peak hour traffic…", more minutes than before (about 1.5×). The fare is the same or higher (see the note above).
- [ ] **K5** **Rain:** remove the peak setting, then set `"Weather": { "ForceRain": true }` (a testing switch: it reports rain everywhere) and restart → note says "…because of rain", minutes about 1.3×.
- [ ] **K6** Both at once → "peak hour traffic and rain", minutes about 1.95×.
- [ ] **K7** Request the ride in time mode: the rider's waiting screen says "· charged by travel time"; the driver's request card says "Charged by travel time (peak hour or rain)…".
- [ ] **K8** Complete a time-priced trip: the final fare comes from the **measured** trip time (start to complete). It is never below the distance fare and never above 1.5× the estimate (so completing instantly gives the distance fare).
- [ ] **K9** Turn the settings back (remove `PeakHours` / `ForceRain`), restart.
- [ ] **K10** **Routing down:** turn off the internet (or block `router.project-osrm.org`) and ask for a quote → it still works, the line is missing, and a red note says "Route unavailable right now: distance is approximate." (distance ≈ straight line × 1.3).
- [ ] **K11** **Weather down** (block `api.open-meteo.com`): the quote works and says "Weather unavailable: priced as dry weather."
- [ ] **K12** Ask for the same quote twice quickly → the second is instant (routes and weather are cached for 10 minutes).
- [ ] **K13** Drop-off closer than ~200 m or farther than 100 km → the same friendly errors as before.

## L. Driver subscriptions (phase 5a: rules, trial, admin grant; no online payment yet)

Before you start: stop the API, run `dotnet ef database update` (adds the subscription columns), start it again.
To test expiry quickly, set `Subscription__TrialDays` to `1` and `Subscription__GraceDays` to `0` (or edit the dates in the `Subscriptions` table).

- [ ] **L1** Approve a new driver in the admin portal. In the driver app, open the home screen: a **free trial** starts (30 days by default) and **Go online** works. The ribbon icon (medal) opens **Your subscription**: "Free trial active · Valid until … · 29 days left", price ₹499 / month, history shows one *Free trial* line.
- [ ] **L2** Reload / reopen the app: still one trial line (a driver never gets a second trial).
- [ ] **L3** Admin portal → **Subscriptions**: the driver appears under *Active* and *On free trial*. The grey line at the top shows the plan numbers in force.
- [ ] **L4** **Add days** on that driver with no reason → "Add a short reason." With 10 days + a reason → green "Added 10 days…", *Valid until* moves out by 10 days, *Last period* shows *Granted* with your reason. The driver app history shows "Added by Samasarathi" with the reason.
- [ ] **L4b** Driver with 1–3 days left (trial or paid): the home banner is **amber** (not blue) with "Your free trial is ending", the days left and "Contact support to renew." Tap it → the status card on the Subscription screen is amber too. Check light and dark mode: the text stays readable. Grace/expired/none banners stay **red**.
- [ ] **L5** Set `Subscription__TrialDays=1`, `GraceDays=3`, approve another driver, wait a day (or move the dates in the database so the period ended yesterday): the driver app shows a red banner "Subscription ended: renew now … keep working until …". **Go online** still works (grace).
- [ ] **L6** Move the period so it ended 4+ days ago: banner "Subscription ended", the button says **Renew your subscription to go online** and is disabled. API check: `POST /api/drivers/me/online` → **409** "Your subscription ended on …".
- [ ] **L7** A driver who is online when the subscription runs out is taken offline within ~10 minutes (the background job) or on their next location update (409), and disappears from the rider's map. A driver in the middle of a ride is **not** cut off until the ride ends.
- [ ] **L8** Rider app: a driver with an ended subscription is not listed nearby, and `POST /api/rides` for them → **409** "This driver isn't available any more".
- [ ] **L9** Admin → Subscriptions → **Expired** tab lists them; **Add days** brings them back (starts today, not from the old end date). Driver app: pull to refresh → banner gone, **Go online** works.
- [ ] **L10** Early renewal: for a driver with 5 days left, **Add days 30** → *Valid until* is the old end + 30 (days are not wasted).
- [ ] **L11** Set `Subscription__Enabled=false`, restart the API: the driver screen says "No subscription needed", everyone can go online, the admin page says subscriptions are switched off. Turn it back on afterwards.
- [ ] **L12** `Subscription__TrialDays=0`, approve a driver: no trial; the driver app says "No subscription yet. Contact support…", Go online is refused (409), the admin page lists them under **Not started** until you add days.
- [ ] **L14** Mid-ride expiry: with a driver in a ride that is already *accepted* or *in progress*, move their subscription end into the past (past grace). The trip keeps working (arrived, start, **Complete trip**); about 5 s after completing they are taken offline. Then, for a ride still waiting for an answer (**requested**), expire the subscription and tap **Accept** → red message "Your subscription ended on … Renew it to accept rides." (409); **Decline** still works.
- [ ] **L13** Change `Subscription__MonthlyPrice` (e.g. `599`) and restart the API: the driver app shows ₹599 with no app update.

## M. Driver ratings for riders, and suspending drivers

- [ ] **M1** Rider app, **Book a ride**: each driver row has a small chip. A driver nobody has rated shows "☆ New driver · Verified"; with 1-4 ratings it shows "★ 4.5 · 2 ratings" (or "1 rating"); with 5 or more it shows "★ 4.8 (23)". Tap the chip: a short dialog explains it (new and verified / only a few ratings so it may change / average of N ratings). Book a driver: the tracking card shows the same chip next to their name. (To test the rated cases, complete and rate rides for one driver.)
- [ ] **M2** Admin portal → open that driver: the left card has a **Rating** line (stars, average, count) and a **Recent ratings from riders** list with the comments.
- [ ] **M3** Open an *Approved* driver → **Suspend driver** with no reason → error asking for a longer reason. With a real reason → confirm → red "Suspended" banner with your reason; the driver now appears under the new **Suspended** tab on the drivers list.
- [ ] **M4** Driver app (suspended driver): pull to refresh → red card "Suspended" with your message and "Contact Samasarathi support…". The map and **Go online** are gone. API check: `POST /api/drivers/me/online` → **409** "Your account is suspended. <your reason> Contact support."
- [ ] **M5** Suspend a driver who is **online and idle**: they disappear from the rider's map right away (no waiting for the 5 s update or the 10 min job).
- [ ] **M6** Suspend a driver who is **in a ride** (accepted or in progress): the trip keeps working and can be completed; within about 5 s after it ends they are taken offline with "Your account was suspended…". A *requested* ride they have not answered cannot be accepted ("Your account is suspended…").
- [ ] **M7** Suspended drivers are not listed for riders and `POST /api/rides` for them → **409** "This driver isn't available any more".
- [ ] **M8** Admin: **Reinstate driver** → confirm → status Approved again, green banner. The driver app (pull to refresh) shows the map; **Go online** works; their subscription and ratings are unchanged.
- [ ] **M9** Only approved drivers can be suspended and only suspended ones reinstated: a driver *In review* has neither button; `POST /api/admin/drivers/{id}/suspend` on one → **400**.

## N. Contacting support

Setup: in `backend/src/Samasarathi.Api/appsettings.Development.json` add (restart the API; placeholders are fine for testing):
`"Support": { "Phone": "+910000000000", "WhatsApp": "+910000000000", "Email": "support@example.com", "Hours": "Mon-Sat, 9 am to 7 pm" }`

- [ ] **N1** `GET /api/support` (no login needed) returns the phone, whatsApp, email and hours you configured.
- [ ] **N2** Driver app (approved): the headset icon in the top bar opens a sheet with **Call us**, **WhatsApp**, **Email** and the hours.
- [ ] **N3** Tap **WhatsApp**: the WhatsApp chat opens with "Hi Samasarathi support, I am <name> (<phone>), a driver." already typed. **Email** opens a draft with the same text; **Call us** opens the dialer with the number.
- [ ] **N4** Driver with a subscription ending, expired or in grace → Subscription screen → **Contact support to renew** opens the same sheet, and the message ends with "I would like to renew my Monthly subscription."
- [ ] **N5** Suspended driver: the red card has a **Contact support** button (message mentions the suspension).
- [ ] **N6** Rider app: home screen → **Contact support** card opens the sheet. During or after a ride, **Need help with this ride?** opens it with the driver's name, vehicle number and ride id in the message.
- [ ] **N7** Remove one value (say the email) and restart the API: that row disappears from the sheet. Remove all three: the sheet says "Support contact details are not available yet". Stop the API: it says "We could not load our contact details".
- [ ] **N8** (real phone) On a device with no WhatsApp installed, tapping WhatsApp shows "Could not open WhatsApp on this phone" (the app does not crash).

---

---

**Known, not bugs:** the red DEBUG ribbon (debug builds only); the map tiles and the destination search use free OpenStreetMap services (development only: their policies rule out production traffic, so before launch switch to Google Maps / Ola Maps / a paid provider); there is **no live-traffic data yet**: peak hour comes from fixed time windows (`PeakHours`) and rain from Open-Meteo, and they slow the expected trip by fixed factors (`Traffic`: 1.5× peak, 1.3× rain); the route's minutes are free-flow. A traffic-aware provider (Google Routes / Ola Maps) replaces this later; the trip distance is the estimate (no GPS trace yet), so the final fare differs from the estimate only for time-priced trips; online payment of the subscription is not built yet (drivers are asked to contact support, and our team adds days from the admin portal).
