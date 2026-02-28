# Travel Planning Skill

A comprehensive Claude skill for travel planning covering flight logistics, authentic local shopping, and activity planning.

## What This Skill Does

### Mode 1: Travel Logistics
- Analyzes flight itineraries and flags risky connections
- Provides airport-specific navigation guidance
- Helps choose between flying and road trips
- Offers TSA/customs preparation assistance
- Gives day-of execution support for delays and missed connections

### Mode 2: Local Shopping Discovery
- Identifies authentic local makers vs tourist traps
- Provides brand comparisons and quality assessment guides
- Helps find reputable shops near your accommodation
- Sets realistic price expectations
- Teaches what to look for when evaluating craftsmanship

### Mode 3: Activity Planning
- Filters activities by user preferences (self-paced vs guided, etc.)
- Provides crowd avoidance strategies
- Gives realistic time estimates including transit
- Identifies drop-in friendly vs must-book experiences
- Builds balanced itineraries respecting user constraints

## Installation

1. Copy the entire `travel-planning/` directory into your Claude skills directory (e.g., `~/.claude/skills/` or `.claude/skills/` in your project)
2. The skill will automatically be available for Claude to use in your conversations

## Usage Examples

### Flight Analysis
```
User: "Here's my flight itinerary [screenshot]"
Claude: [Analyzes using Mode 1, flags tight connections, provides airport guidance]
```

### Local Shopping
```
User: "Where can I buy an authentic ukulele in Honolulu?"
Claude: [Uses Mode 2 to explain brands, find shops, teach quality assessment]
```

### Activity Planning
```
User: "What should I do tomorrow? I don't like being trapped in long tours."
Claude: [Uses Mode 3 to suggest self-paced activities matching preferences]
```

## How It Works

The skill provides structured guidance across three modes:

1. **Travel Logistics** - Systematic flight/road trip analysis with risk flagging
2. **Local Shopping** - Framework for finding authentic products and avoiding tourist traps
3. **Activity Planning** - Filtering system based on user preferences and constraints

Claude fluidly switches between modes based on conversation needs and can combine multiple modes in a single response when relevant.

## Design Philosophy

- **Practical over perfect** - Realistic advice that accounts for real-world constraints
- **Agency-preserving** - Provides information, lets user decide
- **Authenticity-focused** - Prioritizes genuine local experiences over tourist traps
- **Flexible guidance** - Works with or without external data sources

## Contributing

To improve this skill:
- Add destination-specific knowledge (airports, local makers, attractions)
- Update brand comparisons and shopping guides
- Add external tool integrations
- Share what worked/didn't work from actual trips

## License

MIT License. See the root `LICENSE` file for full terms.

## Version

1.0.0 - Initial release based on real travel experience and pain points
