using Flux.Data;

namespace Flux;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Set our view from the "main" layout resource
        SetContentView(Resource.Layout.activity_main);

        var exerciseDatabase = new FakeExerciseDatabase();
        TextView? databaseStatus = FindViewById<TextView>(Resource.Id.database_status);

        if (databaseStatus is not null)
        {
            databaseStatus.Text = $"{exerciseDatabase.Exercises.Count} fake exercises ready\n" +
                                  "10 per dominant region";
        }
    }
}
