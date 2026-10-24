[v1.1 · Premium] Spec 4 + 8.1. Prereq: P10 (tasks), P19.
1. Migration: habit(id,title,color,icon,rrule,target_time,created,archived) and habit_log(habit_id,date,done). Feature.HabitTracker (free = No).
2. A task list can be flagged "Habit". Daily/weekly recurring tasks marked as habits get a streak: current streak, best streak, completion % last 30 days. Streak logic must respect the RRULE (a skipped non-scheduled day does not break the streak) and the device time zone.
3. HabitsPage: list with today's tick box, streak flame icon + number, 7-day dot row, tap -> detail with month heat-grid (Win11-style, tokens only) and edit.
4. Habits with a time create a normal reminder through the existing reminder engine (do not build a second scheduler).
5. Narrator: "Drink water, streak 5 days, not done today".
Tests: streak algorithm (weekly rule, DST day, missed day, back-fill yesterday), gating. Commit "feat: habit tracker".
