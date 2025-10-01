public enum FirebaseEventMKTEnum
{
    ad_impression,
    show_interstitial_ads,
    show_interstitial_ads_suscces,
    show_interstitial_ads_click,
    show_rewarded_ads,
    show_rewarded_ads_sucess,
    show_rewarded_ads_click,
    show_aoa_ads,
    show_aoa_ads_sucess,
    show_aoa_ads_click,
    level_start,
    level_passed,
    level_failed, 
    level_restart,
    level_teleport,
    popup_shop_walk,
    popup_checkpoint,
    btn_booster_jetpack,
    popup_jetpack_get,
    btn_booster_jumpHigh,
    btn_booster_shield,
    popup_revive_get,
    popup_skip_get,
    popup_skip_no_thank,
    popup_revive_no_thank,
    btn_home_end,
    popup_shop_click,
    add_heart,
    full_heart,
    popup_shop_checkpoint,
    popup_shop_click_unlock,
    popup_shop_walk_unlock,
    skin,
    level_1,
    level_start_guess,
    popup_revive_guess_get,
    choose_map,
    shop_open,
    btn_heal
}

public enum FirebaseParamMKTEnum
{
    ad_unit_id, ad_platform, ad_source, ad_unit_name, currency, value,
    placement, country_code, ad_format, level, time_played, tele, placement_lv, use,
    buy, choose_map,
    unlock
}

public enum MyFirebaseEventEnum
{
    my_level_start,
    my_level_passed,
    my_level_failed,
    my_fov,
    my_lookSpeed,
    my_old_level,
}

public enum MyFirebaseParamEnum
{
    level_count_play, time_played, count_play, old_level, fov, lookX, lookY
}