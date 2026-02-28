---
name: travel-planning
description: Comprehensive travel planning covering flight logistics, local shopping discovery, and activity planning
disable-model-invocation: true
---

# Travel Planning Skill

## Overview
Comprehensive travel planning assistance covering flight/road trip logistics, authentic local shopping discovery, and activity planning. This skill helps optimize travel experiences by flagging potential issues, finding quality local products, and filtering activities based on user preferences.


## Mode 1: Travel Logistics

### When to Use
- Analyzing flight itineraries
- Evaluating road trip vs flying decisions
- Airport navigation and connection planning
- Day-of travel execution
- TSA/customs preparation
- Baggage strategy

### Flight Itinerary Analysis

#### Connection Risk Assessment
When analyzing layovers, flag as risky if:
- **< 60 minutes domestic**: Nearly impossible, will likely miss connection
- **60-90 minutes domestic**: Tight, depends heavily on airport and terminals
- **< 90 minutes international (no customs)**: Risky in large airports
- **< 2 hours international (with customs)**: High risk of missing connection
- **Different terminals**: Add 10-30 minutes for terminal transfers

#### Airport-Specific Guidance
Provide context based on the airport:

**Large/Complex Airports (require more buffer time):**
- LAX, JFK, ORD, ATL, DFW, LHR, CDG - add 15-30min to connection estimates
- Note: Multiple terminals often require train/shuttle transfers

**Medium Airports:**
- Most US hubs - standard timing usually sufficient

**Small Airports:**
- Regional airports - connections typically easier, less walking

#### Key Information to Extract
From any flight itinerary, identify and highlight:
1. **Departure time and airport/terminal/gate**
2. **Arrival time and airport/terminal** (with time zone conversion)
3. **Layover duration** (calculate precisely, accounting for time zones)
4. **Terminal changes** (requires extra time)
5. **International connections** (customs/immigration time needed)
6. **Airline changes** (may need to re-check bags, different terminals)
7. **Overnight connections** (hotel needed)

#### Pre-Departure Checklist
Generate based on specific trip:
- **Check-in timing:** 2-3 hours domestic without PreCheck, 3-4 hours international
- **Security considerations:** PreCheck status, liquids rules, electronics
- **Baggage strategy:** Check vs carry-on based on connection times
- **Documentation:** Passport, visa, boarding passes
- **Special items:** Medical devices, batteries (carry-on only), restricted items

#### Day-Of Execution Guidance
When user is at airport or encountering issues:
- **Delayed first flight:** Calculate impact on connections, suggest proactive rebooking
- **Missed connection:** Explain airline obligations, rebooking process, hotel vouchers
- **Gate changes:** Help navigate to new gate quickly
- **Security wait times:** Estimate based on time of day and airport size

### Road Trip vs Flying Analysis

#### When to Recommend Driving
Consider road trip when:
- **Distance < 300 miles:** Usually faster door-to-door when accounting for airport time
- **300-500 miles:** Competitive if traveling with family/group or lots of luggage
- **Multiple destinations:** Road trip allows flexibility for stops
- **Remote destinations:** Poor flight options or expensive regional flights
- **Travel party size:** 3+ people can make driving more economical

#### Factors to Calculate
- **Driving time:** Distance ÷ average speed (typically 60mph highway, 40mph mixed)
- **Flight time:** Flight duration + airport arrival (2hrs) + transit to/from airports + layovers
- **Cost comparison:** 
  - Driving: (miles ÷ MPG × gas price) + tolls + parking
  - Flying: Tickets × passengers + baggage fees + airport parking/rideshare
- **Flexibility:** Road trips allow spontaneous stops, schedule changes
- **Fatigue:** Long drives (6+ hours) can be exhausting; consider overnight stops

### TSA and Customs Rules Quick Reference

#### Carry-On Restrictions
- **Liquids:** 3.4oz (100ml) containers in 1 quart-sized bag
- **Electronics:** Laptops, tablets must be removed for screening (unless PreCheck)
- **Batteries:** Lithium batteries, vapes, power banks MUST be in carry-on (fire hazard in checked bags)
- **Sharp objects:** No knives, scissors >4 inches, tools
- **Prohibited:** Firearms (without proper declaration), explosives, flammable items

#### Checked Bag Rules
- **Weight limits:** Typically 50lbs (23kg), fees for overweight
- **Size limits:** 62 linear inches (length + width + height)
- **Prohibited:** Lithium batteries, vapes, flammable items, compressed gases
- **Valuables:** Don't check electronics, jewelry, medications, documents

#### International Travel
- **Passport validity:** Many countries require 6 months validity beyond travel dates
- **Visas:** Check requirements for destination country
- **Customs declarations:** Must declare items over duty-free limits
- **Agricultural restrictions:** Many countries prohibit fresh foods, plants

---

## Mode 2: Local Shopping Discovery

### When to Use
- Finding authentic local products and makers
- Avoiding tourist traps
- Understanding quality indicators
- Brand comparisons
- Price expectations

### Authentic vs Tourist Trap Detection

#### Red Flags for Tourist Traps
- Located in high-traffic tourist areas (airports, cruise terminals, hotel gift shops)
- "Hawaiian Made" stickers on items clearly mass-produced overseas
- Identical inventory across multiple shops
- Pushy sales tactics or pressure to buy
- Prices seem too cheap for claimed quality (e.g., "solid koa ukulele $150")
- No information about maker/origin
- Accepts only cash with no receipts or clear pricing (harder to dispute charges or return items)

#### Green Flags for Authentic Local Makers
- Located in residential/local neighborhoods, not tourist zones
- Staff can explain maker's background, process, materials
- Items show individual variation (hand-made vs factory)
- Reasonable prices reflecting actual craftsmanship
- Established reputation, reviews from locals
- Maker information visible (name, location, story)
- Willing to show workshop or explain process

### Product Category Guidelines

#### Musical Instruments (e.g., Ukuleles)
**What to look for:**
- Solid wood vs laminate (look inside sound hole - solid wood grain continues through)
- Local wood species (koa for Hawaii, maple for other regions)
- Hand-made details (smooth frets, tight joints, quality tuners)
- Brand reputation (research the "Big 4" or equivalent for region)
- Setup quality (plays in tune up the neck, comfortable action)

**Price expectations:**
- Entry-level solid wood: $300-600
- Mid-range local maker: $600-1200
- High-end artisan: $1200-3000+
- If it seems too cheap, it probably is

**Questions to ask:**
- Where was this made?
- Is this solid wood or laminate?
- Who is the luthier/maker?
- What's the wood species?
- What's your return policy?

#### Art and Crafts
**What to look for:**
- Artist signature or maker's mark
- Certificate of authenticity for expensive pieces
- Traditional techniques vs modern reproductions
- Local materials and motifs
- Gallery representation vs street vendor

#### Food and Consumables
**What to look for:**
- Made in [region] on packaging
- Small batch vs mass-produced
- Local ingredients
- Appropriate pricing (artisan honey costs more than grocery store honey)
- Proper packaging (professionally labeled, sealed)

### Brand Research Workflow

When user asks about a specific brand or product category:

1. **Search for established makers** in that region/category
2. **Compare key differentiators:**
   - History and heritage
   - Price range
   - Distinctive features
   - Target audience (beginners vs professionals)
   - What each is "known for"
3. **Identify "Big X"** - the 3-5 top respected brands in that category
4. **Price positioning** - entry, mid-range, premium tiers
5. **Where to buy** - official shops, authorized dealers, factory tours

**Example output format:**
```
The "Big 4" Hawaiian ukulele brands:
1. Kamaka - Heritage choice, traditional sound, $800-1500
2. Kanile'a - Modern premium, sustainability focus, $1000-2500
3. Ko'olau - Artisan boutique, deep resonance, $800-2000
4. KoAloha - Player's favorite, unique construction, $800-1800
```

### Shopping Location Strategy

When user provides hotel/accommodation address:
1. **Find authentic shops** within reasonable distance
2. **Prioritize local neighborhoods** over tourist districts
3. **Identify clusters** - areas with multiple quality shops (arts districts, maker spaces)
4. **Calculate transit** - walking, driving, or public transit time from accommodation
5. **Check hours** - many artisan shops have limited hours or by appointment

### Quality Assessment Guides

Teach user what to look for when evaluating items in person:

#### Craftsmanship Indicators
- Attention to detail (clean joints, smooth finishes, consistent stitching)
- Quality materials (solid vs composite, natural vs synthetic)
- Functional excellence (does it work well, not just look good?)
- Finish quality (no rough edges, drips, misalignments)

#### Price Reality Checks
- Hand-made items cost more than factory-made
- Local materials cost more than imported
- Artisan time is valuable
- If price seems "too good to be true" for quality claimed, it probably is

---

## Mode 3: Activity Planning

### When to Use
- Planning museums, tours, attractions
- Filtering by user preferences (self-paced vs guided, line avoidance)
- Time management and crowd avoidance
- Finding drop-in friendly vs must-book experiences

### User Preference Profiling

Identify from conversation or ask about:
- **Pacing preference:** Self-paced vs guided tours vs rushed itineraries
- **Social preference:** Solo exploration vs group activities vs intimate small groups
- **Commitment tolerance:** Drop-in flexibility vs advance booking required
- **Physical constraints:** Walking distance, standing time, accessibility needs
- **Interest depth:** Quick overview vs deep dive vs specialized focus
- **Time flexibility:** Fixed schedule vs flexible timing

### Activity Filtering Framework

#### Self-Paced vs Guided
**Recommend self-paced when user:**
- Dislikes feeling trapped or on a schedule
- Wants to spend variable time at different exhibits
- Prefers to explore at own rhythm
- Values flexibility to leave early or extend

**Self-paced options:**
- Museums with general admission (no timed entry)
- Self-guided walking tours (app or map-based)
- Open-access parks and beaches
- Markets and public spaces
- Audio guide museums

**Avoid or flag as guided:**
- Group tours with fixed schedules
- Timed entry tickets
- Multi-hour commitments
- Tours that require minimum group size

#### Line and Crowd Avoidance

**Strategies to suggest:**
- **Off-peak timing:** Weekday mornings, late afternoons (not midday or weekends)
- **Advance tickets:** Skip-the-line options when available
- **Shoulder season:** Visit popular attractions outside peak tourist season
- **Opening/closing times:** First hour or last hour often less crowded
- **Alternatives:** Lesser-known museums/attractions with similar content

**Red flags to warn about:**
- "Must see" attractions during peak season (expect lines)
- Weekend visits to popular museums
- Midday (11am-2pm) at tourist sites
- Major holidays and school vacation periods

#### Duration Estimation

Provide realistic time estimates including:
- **Transit to/from:** Don't underestimate getting there and back
- **Wait time:** Lines, ticket purchase, security
- **Activity duration:** Conservative estimate (people often linger longer than planned)
- **Buffer time:** Unexpected delays, bathroom breaks, food

**Example:**
```
Museum visit estimate:
- Transit: 20 min each way = 40 min
- Entry/tickets: 15 min (if buying on-site)
- Museum time: 2-3 hours (plan for 3)
- Total commitment: 4 hours
```

#### Drop-In Friendly Filter

**Highly drop-in friendly:**
- Public parks and beaches
- Open-air markets
- Most restaurants (non-reservation)
- Street performances and public art
- Self-guided city walks

**Moderately flexible:**
- Museums with general admission (no timed entry)
- Popular restaurants (may have wait)
- Some attractions with online tickets (same-day availability)

**Must book ahead:**
- Popular tours (often sell out days/weeks in advance)
- Timed entry attractions
- Special exhibitions
- Fine dining restaurants
- Outdoor activities (kayaking, zip-lining, etc.)

### Activity Recommendation Structure

When suggesting activities, format as:

```
**[Activity Name]**
- Type: [Museum/Tour/Experience/etc.]
- Duration: [Realistic time estimate including transit]
- Pacing: [Self-paced/Guided/Mixed]
- Booking: [Drop-in/Advance recommended/Must book]
- Best time: [Specific timing to avoid crowds]
- Why it fits: [How it matches user preferences]
- Caveats: [Any aspects that might not align with preferences]
```

### Itinerary Construction Principles

When building day plans:

1. **Don't overpack:** Better to do 2-3 things well than rush through 6
2. **Geographic clustering:** Group activities in same area to minimize transit
3. **Energy management:** Alternate active/passive, indoors/outdoors
4. **Meal planning:** Don't schedule activities across typical meal times without food plan
5. **Buffer time:** Build in flexibility, things always take longer than expected
6. **Backup options:** Weather-dependent plans need indoor alternatives

**Example day structure:**
```
Morning (9am-12pm): Museum visit (self-paced, 2-3 hours)
Lunch (12pm-1:30pm): Nearby restaurant (local neighborhood)
Afternoon (2pm-5pm): Walking exploration (drop-in markets, galleries)
Evening (6pm+): Flexible - beach sunset, casual dinner, early return
```

### Nightlife as Optional Activity

When nightlife comes up, treat it as one activity option among many:

**Filtering criteria:**
- Vibe: Chill vs energetic, touristy vs local
- Music: Live music type, DJ, or quiet conversation
- Commitment: Can leave anytime vs seated reservation
- Alcohol focus: Bar-centric vs food/entertainment-focused
- Closing time: Early (10pm) vs late (2am+)

**Alternative evening activities** (for users trying to drink less):
- Beach sunsets
- Evening markets or food trucks
- Live music without bar focus
- Outdoor movies or cultural performances
- Scenic walks or viewpoints
- Coffee shops with evening hours

---

## Workflow Integration

### Typical User Journey

**Pre-Trip Planning:**
1. User shares flight itinerary → Analyze with Mode 1 (logistics)
2. Identify potential issues, suggest fixes
3. Ask about interests → Switch to Mode 2 (shopping) and Mode 3 (activities)
4. Research authentic local shops and plan activities
5. Create day-by-day itinerary incorporating logistics, shopping, activities

**During Trip:**
1. User encounters issue (delay, missed connection) → Mode 1 assistance
2. User asks "what should I do tonight?" → Mode 3 with real-time considerations
3. User needs to find specific item → Mode 2 location and quality guidance

**Post-Trip:**
1. Debrief for future trip planning
2. Note what worked/didn't work for user's preferences
3. Archive learnings for next trip

### Mode Switching

Claude should fluidly switch between modes based on user needs:
- Flight delay question → Mode 1
- "Where can I buy authentic X?" → Mode 2  
- "What should I do this afternoon?" → Mode 3
- Can handle multiple modes in single response when relevant

### Proactive Guidance

Don't wait for user to ask - proactively flag:
- **Tight connections** when analyzing itinerary
- **Tourist traps** when user mentions shopping location
- **Crowd timing** when user mentions popular attraction
- **Weather impacts** on planned activities
- **Transportation logistics** between activities

---

## Best Practices

### Information Gathering
- Ask for screenshots/confirmations rather than having user manually type details
- Confirm time zones when discussing schedules
- Verify user's location/accommodation for proximity calculations
- Understand user's constraints (budget, time, preferences) early

### Advice Calibration
- Be honest about risks (tight connections, tourist traps)
- Provide realistic time estimates (people underestimate travel time)
- Explain reasoning, don't just give directives
- Offer alternatives when primary plan has issues

### Tone and Approach
- Practical and direct, not overly cautious or alarmist
- Empathetic to travel stress and fatigue
- Encouraging of authentic experiences over tourist traps
- Respectful of user's agency and preferences

### When to Push Back
- Gently challenge unrealistic itineraries (too many activities, too little buffer)
- Flag genuinely risky travel plans (very tight connections, sketchy neighborhoods)
- Suggest alternatives when user's plan has obvious issues
- But ultimately respect user's decisions - provide information, let them choose

---

## Maintenance and Updates

This skill benefits from:
- Regular updates to known good shops/makers in popular destinations
- New external tool integrations as they become available
- User feedback on what worked/didn't work
- Evolving best practices for specific airports/destinations

When encountering new destinations or categories, research thoroughly and add patterns to this skill for future use.
