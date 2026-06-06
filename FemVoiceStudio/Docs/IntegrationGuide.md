# Smart Coach Integration Guide

## Overview
Smart Coach is an intelligent coaching system that provides individualized, evidence-based guidance for voice feminization training users. The system analyzes training data from the existing SQLite database and generates personalized recommendations.

## Architecture

### Core Components

1. **SmartCoachEngine.cs** - Core logic engine
   - Baseline calculation algorithm
   - Progression assessment
   - Recommendation generation
   - Health monitoring

2. **SmartCoachViewModel.cs** - MVVM ViewModel
   - Data binding for UI
   - User interaction handling
   - Data refresh logic

3. **SmartCoachDashboardView.xaml** - Compact dashboard component
   - Today's focus display
   - Quick stats (streak, sessions, health)
   - Health warning indicators

4. **SmartCoachDetailView.xaml** - Detailed Smart Coach page
   - Full progress overview
   - Weekly history
   - Goals tracking
   - Messages

### Database Tables

The following tables are automatically created in the SQLite database:

```sql
-- Smart Coach baseline (calculated after first weeks)
SmartCoachBaseline

-- Individual goals
SmartCoachGoals

-- Daily recommendations
SmartCoachDailyRecommendations

-- Weekly progress tracking
SmartCoachWeeklyProgress

-- Health monitoring
SmartCoachHealthMonitoring

-- Coach messages (motivation, tips, warnings)
SmartCoachMessages
```

## Integration with Existing Systems

### 1. Training Calendar Integration

The Smart Coach integrates with the existing training calendar through the `DatabaseService`:

```csharp
var weeklyProgress = _database.GetTrainingStats(weekStart, weekEnd);
```

**Location in code:** `SmartCoachEngine.CalculateWeeklyProgress()`

### 2. Analysis Engine Integration

After each training session analysis, call the health monitoring:

```csharp
// After saving a training session
var engine = new SmartCoachEngine(database);
var health = engine.AnalyzeSessionForStrain(session);
```

**Location in code:** Should be called from `MainViewModel.StopRecording()`

### 3. Feedback System Integration

The Smart Coach recommendations are generated based on the same analysis data used by the feedback system. The `TrainingSession` model includes:
- `AveragePitch`, `PitchVariation`
- `ResonanceScore`, `AverageF1`, `AverageF2`
- `IntonationScore`

## Clinical Best Practices

### Evidence-Based Priority Order

1. **Resonance first** (highest priority)
   - Forward resonance is prioritized over isolated pitch training
   - Target: F1/F2 optimization for feminine sound
   
2. **Pitch control**
   - Stable pitch is more important than reaching high pitch
   - Avoid pitch press (strain detection)

3. **Intonation variation**
   - Monotonic speech is a common issue
   - Natural variation is key for feminine voice

4. **Breathing support**
   - Diaphragmatic breathing for voice support
   - Prevents fatigue

### Health Monitoring Thresholds

| Metric | Threshold | Action |
|--------|-----------|--------|
| Pitch Press | > 180 Hz | Warning triggered |
| Score Drop | > 20% | Fatigue detection |
| RMS Variance | > 0.3 | Noise detection |

## Usage

### Basic Integration

```csharp
// Initialize in your ViewModel
var database = new DatabaseService();
var engine = new SmartCoachEngine(database);

// Get daily recommendation
var recommendation = engine.GenerateDailyRecommendation();

// Display in UI
ViewModel.TodayFocus = recommendation.FocusArea;
ViewModel.TodayRecommendation = recommendation.RecommendationText;
```

### Opening the Detail View

```csharp
var detailWindow = new SmartCoachDetailWindow();
detailWindow.Show();
```

### Using the Dashboard Component

```xml
<views:SmartCoachDashboardView/>
```

## Configuration

### Customizable Thresholds

Edit in `SmartCoachEngine.cs`:

```csharp
private const double ResonancePriorityThreshold = 70.0;
private const double PitchPressThreshold = 180.0;
private const double FatigueScoreDrop = 20.0;
private const int BaselineMinDays = 7;
```

### Recommendation Text Customization

Edit recommendation methods in `SmartCoachEngine.cs`:
- `GetResonanceRecommendation()`
- `GetPitchControlRecommendation()`
- `GetIntonationRecommendation()`
- `GetRecoveryRecommendation()`

## Testing

### Unit Tests

Test the core algorithms:

```csharp
// Test baseline calculation
var engine = new SmartCoachEngine(database);
var baseline = engine.CalculateBaseline();

// Test recommendation generation
var recommendation = engine.GenerateDailyRecommendation();

// Test health monitoring
var health = engine.AnalyzeSessionForStrain(session);
```

### Integration Tests

1. Create a test user with multiple training sessions
2. Verify baseline calculation after 7+ days
3. Verify recommendations are personalized
4. Test health warning triggers

## Data Flow

```
Training Session → Database → SmartCoachEngine
                                    ↓
                    ┌───────────────┼───────────────┐
                    ↓               ↓               ↓
              Baseline       Recommendations    Health
                ↓               ↓
            Goals           Weekly Progress
                ↓               ↓
            UI ←←←←←←←←←←←←←←←←
```

## Maintenance

### Regular Updates

- **Weekly**: Recalculate weekly progress
- **Monthly**: Regenerate goals based on progress
- **Ongoing**: Health monitoring after each session

### Database Cleanup

Old data can be cleaned up using:

```csharp
// Keep only last 90 days of health monitoring
var oldRecords = database.GetRecentHealthIssues(90);
foreach (var record in oldRecords) {
    // Delete old records
}
```

## Troubleshooting

### No Baseline Calculated
- **Cause**: Less than 3 training sessions
- **Solution**: Continue training until baseline is established

### Recommendations Not Updating
- **Cause**: Already generated for today
- **Solution**: Delete today's recommendation from database or wait for tomorrow

### Health Warnings Not Showing
- **Cause**: Session analysis not called after training
- **Solution**: Ensure `AnalyzeSessionForStrain()` is called in the training workflow
