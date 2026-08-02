@{
    CatalogAdditionIds = @(32, 37, 41, 47, 56, 58, 59, 60, 167, 625)

    # Runtime-facing labels for exercises whose lineage cannot be recovered
    # reliably from the display name alone.
    CatalogPracticeOverrides = @{
        31 = 'Tai Chi'
        32 = 'Gait retraining'
        37 = 'Low-impact aerobics'
        41 = 'Backward walking'
        47 = 'Vestibular rehabilitation'
        56 = 'Hula'
        58 = 'Bhangra'
        59 = 'Samba'
        60 = 'Low-impact aerobics'
        108 = 'Ballet'
        167 = 'Running drills'
        187 = 'Ballet'
        188 = 'Ballet'
        197 = 'Ballet'
        198 = 'Ballet'
        389 = 'Ballet'
        409 = 'Functional Range Conditioning'
        467 = 'Qigong'
        475 = 'Jazz dance'
        625 = 'Wing Chun'
        626 = 'Sumo'
        960 = 'Fundamental movement skills'
        969 = 'Yoga'
    }

    # Default catalog-label to DAG-node mapping. Broad nodes are intentional
    # where the legacy label does not preserve a defensible specific lineage.
    DagMappings = @{
        'Balance training' = @('fitness')
        'Ballet' = @('ballet')
        'Backward walking' = @('backward_walking')
        'Belly dance' = @('belly_dance_raqs_sharqi')
        'Bhangra' = @('bhangra')
        'Bharatanatyam' = @('bharatanatyam')
        'Bodyweight conditioning' = @('calisthenics')
        'Boxing' = @('boxing')
        'Capoeira' = @('capoeira')
        'Functional Range Conditioning' = @('functional_range_conditioning')
        'Fundamental movement skills' = @('fundamental_movement_skills')
        'Gait retraining' = @('gait_retraining')
        'Hand therapy and mobility' = @('healing_rehab')
        'Hula' = @('hula')
        'Jazz dance' = @('jazz_dance')
        'Karate' = @('karate')
        'Low-impact aerobics' = @('aerobics')
        'Ninja hand-seal coordination' = @()
        'Odissi' = @('odissi')
        'Pilates' = @('somatics_pilates')
        'Qigong' = @('qigong')
        'Running drills' = @('running')
        'Samba' = @('samba')
        'Self-resistance' = @('fitness_resistance')
        'Standing mobility and movement practice' = @('fitness')
        'Stretching' = @('fitness')
        'Sumo' = @('sumo')
        'Taekwondo' = @('taekwondo')
        'Tai Chi' = @('taijiquan')
        'Vestibular rehabilitation' = @('vestibular_rehabilitation')
        'Wing Chun' = @('wing_chun')
        'Yoga' = @('somatics_yoga')
    }
}
